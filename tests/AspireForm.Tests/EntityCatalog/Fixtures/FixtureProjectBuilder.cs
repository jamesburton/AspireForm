namespace AspireForm.Tests.EntityCatalog.Fixtures;

internal sealed class FixtureProjectBuilder : IDisposable
{
    public string Root { get; }
    public string CsprojPath { get; }

    public FixtureProjectBuilder(string testName)
    {
        Root = Path.Combine(Path.GetTempPath(), $"af-fix-{testName}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
        CsprojPath = Path.Combine(Root, $"{testName}.csproj");
        File.WriteAllText(CsprojPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
              </PropertyGroup>
            </Project>
            """);
    }

    public string AddFile(string relativePath, string content)
    {
        var abs = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
        File.WriteAllText(abs, content);
        return abs;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { /* best-effort */ }
    }
}
