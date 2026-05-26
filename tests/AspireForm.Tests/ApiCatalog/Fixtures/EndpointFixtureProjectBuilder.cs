namespace AspireForm.Tests.ApiCatalog.Fixtures;

/// <summary>Creates a temporary .NET class-library project with stub <c>[ApiEndpoint]</c> attributes for scanner/mutator fixture tests.</summary>
internal sealed class EndpointFixtureProjectBuilder : IDisposable
{
    /// <summary>Root directory of the fixture project.</summary>
    public string Root { get; }

    /// <summary>Absolute path to the fixture <c>.csproj</c> file.</summary>
    public string CsprojPath { get; }

    /// <summary>Inline stub source for all API endpoint attributes (added once per fixture project).</summary>
    private const string AttributeStubs = """
        // Stubs — replace AspireForm.Annotations reference in fixture projects.
        namespace AspireForm.Annotations
        {
            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
            public sealed class ApiEndpointAttribute : System.Attribute
            {
                public ApiEndpointAttribute(string route, string method = "GET") { Route = route; Method = method; }
                public string Route { get; }
                public string Method { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
            public sealed class ApiAuthAttribute : System.Attribute
            {
                public ApiAuthAttribute(string policy) { Policy = policy; }
                public string Policy { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
            public sealed class ApiTagAttribute : System.Attribute
            {
                public ApiTagAttribute(string tag) { Tag = tag; }
                public string Tag { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
            public sealed class ApiSummaryAttribute : System.Attribute
            {
                public ApiSummaryAttribute(string summary) { Summary = summary; }
                public string Summary { get; }
            }
        }
        """;

    /// <summary>Creates a fixture project under a uniquely-named temp directory.</summary>
    /// <param name="testName">Short name used in the directory name to ease debugging.</param>
    public EndpointFixtureProjectBuilder(string testName)
    {
        Root = Path.Combine(Path.GetTempPath(), $"af-ep-fix-{testName}-{Guid.NewGuid():N}");
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

        // Add attribute stubs as a shared file so all other fixture files can use them.
        AddFile("_AttributeStubs.cs", AttributeStubs);
    }

    /// <summary>Writes a source file at <paramref name="relativePath"/> (relative to the project root) with <paramref name="content"/>.</summary>
    /// <returns>The absolute path to the written file.</returns>
    public string AddFile(string relativePath, string content)
    {
        var abs = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
        File.WriteAllText(abs, content);
        return abs;
    }

    /// <summary>Adds a handler source file decorated with <c>[ApiEndpoint]</c>. The attribute stubs are already present in the project.</summary>
    /// <param name="relativePath">Relative path within the fixture project.</param>
    /// <param name="handlerSource">Handler class source code (should include using directives and namespace).</param>
    /// <returns>The absolute path to the written file.</returns>
    public string AddEndpointHandlerFile(string relativePath, string handlerSource) =>
        AddFile(relativePath, handlerSource);

    /// <inheritdoc />
    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { /* best-effort */ }
    }
}
