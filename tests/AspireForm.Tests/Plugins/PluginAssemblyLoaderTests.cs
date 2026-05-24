using System.Reflection;
using AspireForm.Plugins;
using AspireForm.Providers;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AspireForm.Tests.Plugins;

public sealed class PluginAssemblyLoaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-plugin-load").FullName;

    /* Newtonsoft.Json 13.0.3 is known to be in the NuGet cache on this machine (net6.0 is the best TFM
       available since there is no net10.0 build in that package version). The test copies the DLL into
       the plugin's lib directory and relies on PluginAssemblyLoader's filename-match fallback. */
    private static readonly string NewtonsoftDllPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages", "newtonsoft.json", "13.0.3", "lib", "net6.0", "Newtonsoft.Json.dll");

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        // Loaded plugin DLLs remain locked by the non-collectible ALC on Windows; the OS cleans the temp dir on reboot.
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    [Fact]
    public void LoadProviders_returns_provider_instances_for_each_manifest_entry()
    {
        var assemblyPath = SynthesizeTestPluginAssembly(_dir, "FakePlugin",
            providerClassName: "FakePlugin.FakeProvider", providerType: "fake-type", kind: "resource");
        var packageDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assemblyPath)!)!)!;
        WriteManifest(packageDir, "Fake", providerType: "fake-type",
            className: "FakePlugin.FakeProvider", assemblyName: "FakePlugin");

        var manifest = PluginManifest.Parse(File.ReadAllText(Path.Combine(packageDir, "aspireform-plugin.json")));

        var loader = new PluginAssemblyLoader();
        var providers = loader.LoadProviders(packageDir, manifest);

        providers.Should().ContainSingle();
        providers[0].Type.Should().Be("fake-type");
        providers[0].Kind.Should().Be(BlockKind.Resource);
    }

    [Fact]
    public void LoadProviders_throws_PluginContractException_when_the_named_class_is_absent()
    {
        var assemblyPath = SynthesizeTestPluginAssembly(_dir, "EmptyPlugin",
            providerClassName: "EmptyPlugin.RealClass", providerType: "ignored", kind: "resource");
        var packageDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assemblyPath)!)!)!;
        WriteManifest(packageDir, "Empty", providerType: "x",
            className: "EmptyPlugin.NoSuchClass", assemblyName: "EmptyPlugin");

        var manifest = PluginManifest.Parse(File.ReadAllText(Path.Combine(packageDir, "aspireform-plugin.json")));

        var loader = new PluginAssemblyLoader();
        var act = () => loader.LoadProviders(packageDir, manifest);
        act.Should().Throw<PluginContractException>().WithMessage("*NoSuchClass*");
    }

    [Fact]
    public void LoadProviders_resolves_transitive_dependency_via_filename_fallback()
    {
        if (!File.Exists(NewtonsoftDllPath))
        {
            // Newtonsoft.Json 13.0.3 is not in the NuGet cache on this machine — skip.
            return;
        }

        /* Synthesise a plugin whose provider calls JsonConvert.SerializeObject at runtime, which forces
           the CLR to load Newtonsoft.Json when the method is JIT-compiled. The DLL is NOT on the default
           ALC probe path, so the Resolving handler in PluginAssemblyLoader must supply it. */
        var assemblyPath = SynthesizeTestPluginAssembly(_dir, "TransitivePlugin",
            providerClassName: "TransitivePlugin.TransitiveProvider",
            providerType: "transitive-test",
            kind: "resource",
            extraDepPath: NewtonsoftDllPath);

        var packageDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assemblyPath)!)!)!;
        WriteManifest(packageDir, "Transitive", providerType: "transitive-test",
            className: "TransitivePlugin.TransitiveProvider", assemblyName: "TransitivePlugin");

        var manifest = PluginManifest.Parse(File.ReadAllText(Path.Combine(packageDir, "aspireform-plugin.json")));

        var loader = new PluginAssemblyLoader();
        var providers = loader.LoadProviders(packageDir, manifest);

        providers.Should().ContainSingle();

        // Invoke the method that calls JsonConvert.SerializeObject via reflection.
        // If the Resolving handler does not fire, this throws FileNotFoundException for Newtonsoft.Json.
        var providerType = providers[0].GetType();
        var method = providerType.GetMethod("SerializeTest", BindingFlags.Public | BindingFlags.Instance)!;
        var result = (string)method.Invoke(providers[0], null)!;
        result.Should().NotBeNullOrEmpty();
    }

    private static string SynthesizeTestPluginAssembly(
        string dir, string assemblyName, string providerClassName, string providerType, string kind,
        string? extraDepPath = null)
    {
        // Layout: <dir>/<assemblyName>/lib/net10.0/<assemblyName>.dll
        var libDir = Path.Combine(dir, assemblyName, "lib", "net10.0");
        Directory.CreateDirectory(libDir);

        var (ns, cls) = providerClassName.Contains('.')
            ? (providerClassName[..providerClassName.LastIndexOf('.')], providerClassName[(providerClassName.LastIndexOf('.') + 1)..])
            : ("", providerClassName);

        // When a dep DLL is provided, copy it alongside the plugin DLL and include a method that
        // uses it — this forces the CLR to actually load the transitive assembly at JIT time.
        var usingClause = extraDepPath is not null ? "using Newtonsoft.Json;" : "";
        var extraMethod = extraDepPath is not null
            ? """
                  public string SerializeTest() => JsonConvert.SerializeObject(new { ok = true });
              """
            : "";

        var source = $$"""
            using AspireForm.Providers;
            using System.Text.Json.Nodes;
            {{usingClause}}

            namespace {{ns}};

            public sealed class {{cls}} : IProvider
            {
                public string Type => "{{providerType}}";
                public BlockKind Kind => BlockKind.{{(kind == "module" ? "Module" : "Resource")}};
                public ProviderPlan Plan(PlanContext context) => new();
            {{extraMethod}}
            }
            """;

        // Filter out assemblies whose Location no longer exists. On Linux CI, temp dirs from
        // previous test iterations may have been cleaned up, leaving stale Location paths that
        // would crash Roslyn's compile when it tries to open them.
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location) && File.Exists(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        // Add the extra dep as a compile-time reference (Roslyn needs it to resolve Newtonsoft.Json types).
        if (extraDepPath is not null)
        {
            refs.Add(MetadataReference.CreateFromFile(extraDepPath));

            // Copy dep DLL next to the plugin DLL — the Resolving fallback looks for <name>.dll in the
            // same directory as any registered plugin assembly.
            File.Copy(extraDepPath, Path.Combine(libDir, Path.GetFileName(extraDepPath)), overwrite: true);
        }

        var syntax = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: [syntax],
            references: refs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var assemblyPath = Path.Combine(libDir, $"{assemblyName}.dll");
        using var stream = File.Create(assemblyPath);
        var emit = compilation.Emit(stream);
        if (!emit.Success)
        {
            var diagnostics = string.Join("\n", emit.Diagnostics.Select(d => d.ToString()));
            throw new InvalidOperationException("Failed to compile test plugin:\n" + diagnostics);
        }
        return assemblyPath;
    }

    private static void WriteManifest(string packageDir, string name, string providerType, string className, string assemblyName)
    {
        File.WriteAllText(Path.Combine(packageDir, "aspireform-plugin.json"), $$"""
            {
              "name": "{{name}}",
              "version": "0.1.0",
              "minAspireFormVersion": "0.3.0",
              "assemblyName": "{{assemblyName}}",
              "providers": [
                { "type": "{{providerType}}", "kind": "resource", "className": "{{className}}" }
              ]
            }
            """);
    }
}
