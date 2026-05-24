using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AspireForm.Plugins;

/// <summary>The outcome of compiling a script plugin.</summary>
/// <param name="Success">True when the compile succeeded and the assembly was written or cached.</param>
/// <param name="AssemblyPath">Absolute path to the compiled assembly; null on failure.</param>
/// <param name="ErrorMessage">Human-readable error description; null on success.</param>
public sealed record ScriptCompileResult(bool Success, string? AssemblyPath, string? ErrorMessage);

/// <summary>Compiles a <c>.cs</c>-script plugin via Roslyn, with NuGet dep restore and source-hash caching.</summary>
public sealed class ScriptPluginCompiler
{
    private readonly PluginRestorer _restorer;

    /// <summary>Initialises the compiler with a default <see cref="PluginRestorer"/>.</summary>
    public ScriptPluginCompiler()
    {
        _restorer = new PluginRestorer();
    }

    /// <summary>
    /// Compiles <paramref name="scriptPath"/> into the project's cache directory.
    /// Returns the cached assembly path if a prior compile of the same source already exists.
    /// </summary>
    /// <param name="scriptPath">Absolute path to the <c>.cs</c> script file to compile.</param>
    /// <param name="projectDir">The AspireForm project root directory (used to locate the cache).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ScriptCompileResult"/> describing success, the assembly path, or a failure message.</returns>
    public async Task<ScriptCompileResult> CompileAsync(
        string scriptPath, string projectDir, CancellationToken cancellationToken = default)
    {
        var source = await File.ReadAllTextAsync(scriptPath, cancellationToken);
        var sourceHash = HashSource(source);
        var assemblyName = Path.GetFileNameWithoutExtension(scriptPath);
        var cacheDir = Path.Combine(projectDir, ".aspireform", "scripts", ".cache", sourceHash);
        var cachedAssemblyPath = Path.Combine(cacheDir, $"{assemblyName}.dll");

        // Return the cached assembly if one already exists for this source hash.
        if (File.Exists(cachedAssemblyPath))
        {
            return new ScriptCompileResult(true, cachedAssemblyPath, null);
        }

        Directory.CreateDirectory(cacheDir);

        // Restore #:package directives and collect references.
        var directives = ScriptDirectiveParser.Parse(source).ToList();
        var references = new List<MetadataReference>();
        references.AddRange(BuiltInReferences());

        foreach (var directive in directives.Where(d => d.Kind == ScriptDirectiveKind.Package))
        {
            var restore = await _restorer.RestoreAsync(directive.PackageId, directive.Version, projectDir, cancellationToken);
            if (!restore.Success)
            {
                return new ScriptCompileResult(false, null,
                    $"Failed to restore '#:package {directive.PackageId}@{directive.Version}': {restore.ErrorMessage}");
            }

            var libDir = Path.Combine(restore.PackageDirectory!, "lib", "net10.0");
            if (Directory.Exists(libDir))
            {
                foreach (var dll in Directory.EnumerateFiles(libDir, "*.dll"))
                {
                    references.Add(MetadataReference.CreateFromFile(dll));
                }
            }
        }

        var syntax = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: [syntax],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using (var stream = File.Create(cachedAssemblyPath))
        {
            var emit = compilation.Emit(stream, cancellationToken: cancellationToken);
            if (!emit.Success)
            {
                stream.Close();
                try { File.Delete(cachedAssemblyPath); } catch { /* ignore */ }
                var diags = string.Join("\n", emit.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));
                return new ScriptCompileResult(false, null, $"Script compile failed:\n{diags}");
            }
        }

        return new ScriptCompileResult(true, cachedAssemblyPath, null);
    }

    /// <summary>
    /// Collects <see cref="MetadataReference"/> entries for all non-dynamic assemblies currently loaded
    /// into the host process. This gives the compiled script access to the full AspireForm type surface
    /// and the .NET BCL without requiring explicit assembly enumeration.
    /// </summary>
    private static IEnumerable<MetadataReference> BuiltInReferences()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location) && File.Exists(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>();
    }

    /// <summary>Returns the lowercase hex SHA-256 hash of the UTF-8 encoded <paramref name="source"/>.</summary>
    private static string HashSource(string source)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return Convert.ToHexStringLower(bytes);
    }
}
