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

    private static string SynthesizeTestPluginAssembly(
        string dir, string assemblyName, string providerClassName, string providerType, string kind)
    {
        // Layout: <dir>/<assemblyName>/lib/net10.0/<assemblyName>.dll
        var libDir = Path.Combine(dir, assemblyName, "lib", "net10.0");
        Directory.CreateDirectory(libDir);

        var (ns, cls) = providerClassName.Contains('.')
            ? (providerClassName[..providerClassName.LastIndexOf('.')], providerClassName[(providerClassName.LastIndexOf('.') + 1)..])
            : ("", providerClassName);

        var source = $$"""
            using AspireForm.Providers;
            using System.Text.Json.Nodes;

            namespace {{ns}};

            public sealed class {{cls}} : IProvider
            {
                public string Type => "{{providerType}}";
                public BlockKind Kind => BlockKind.{{(kind == "module" ? "Module" : "Resource")}};
                public ProviderPlan Plan(PlanContext context) => new();
            }
            """;

        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

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
