using AspireForm.Plugins;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Plugins;

public sealed class ScriptPluginCompilerTests : IDisposable
{
    private readonly string _projectDir = Directory.CreateTempSubdirectory("aspireform-script-compile").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_projectDir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task CompileAsync_emits_an_assembly_implementing_IProvider()
    {
        const string source = """
            using AspireForm.Providers;
            namespace InlineScript;
            public sealed class ScriptProvider : IProvider
            {
                public string Type => "script-test";
                public BlockKind Kind => BlockKind.Resource;
                public ProviderPlan Plan(PlanContext context) => new();
            }
            """;

        var scriptPath = Path.Combine(_projectDir, ".aspireform", "scripts", "test.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        await File.WriteAllTextAsync(scriptPath, source);

        var compiler = new ScriptPluginCompiler();
        var result = await compiler.CompileAsync(scriptPath, _projectDir);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.AssemblyPath.Should().NotBeNull();
        File.Exists(result.AssemblyPath).Should().BeTrue();
    }

    [Fact]
    public async Task CompileAsync_returns_failure_on_invalid_source()
    {
        var scriptPath = Path.Combine(_projectDir, ".aspireform", "scripts", "bad.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        await File.WriteAllTextAsync(scriptPath, "this is not valid C# at all");

        var compiler = new ScriptPluginCompiler();
        var result = await compiler.CompileAsync(scriptPath, _projectDir);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompileAsync_caches_on_source_hash_and_skips_recompile()
    {
        const string source = """
            using AspireForm.Providers;
            namespace CacheTest;
            public sealed class CachedProvider : IProvider
            {
                public string Type => "cached";
                public BlockKind Kind => BlockKind.Resource;
                public ProviderPlan Plan(PlanContext context) => new();
            }
            """;

        var scriptPath = Path.Combine(_projectDir, ".aspireform", "scripts", "cached.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        await File.WriteAllTextAsync(scriptPath, source);

        var compiler = new ScriptPluginCompiler();
        var first = await compiler.CompileAsync(scriptPath, _projectDir);
        var firstWritten = File.GetLastWriteTimeUtc(first.AssemblyPath!);

        await Task.Delay(50);
        var second = await compiler.CompileAsync(scriptPath, _projectDir);
        var secondWritten = File.GetLastWriteTimeUtc(second.AssemblyPath!);

        second.AssemblyPath.Should().Be(first.AssemblyPath);
        secondWritten.Should().Be(firstWritten);
    }
}
