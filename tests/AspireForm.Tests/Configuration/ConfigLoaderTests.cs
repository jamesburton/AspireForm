using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class ConfigLoaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-test").FullName;
    private readonly ConfigLoader _loader = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void WriteFile(string name, string content) => File.WriteAllText(Path.Combine(_dir, name), content);

    [Fact]
    public void Loads_a_yaml_config()
    {
        WriteFile("aspireform.yaml", """
            aspireform:
              version: 1
              project: MyApp
              apphost: ./MyApp.AppHost
            """);

        var loaded = _loader.Load(_dir, env: null);

        loaded.Model.AspireForm.Project.Should().Be("MyApp");
    }

    [Fact]
    public void Layers_an_environment_override_file()
    {
        WriteFile("aspireform.yaml", """
            aspireform:
              version: 1
              project: MyApp
              apphost: ./MyApp.AppHost
            resources:
              sql:
                type: sqlserver
                aspireName: sql
            """);
        WriteFile("aspireform.dev.yaml", """
            resources:
              sql:
                aspireName: sql-dev
            """);

        var loaded = _loader.Load(_dir, env: "dev");

        loaded.Model.Resources["sql"].Inputs["aspireName"]!.GetValue<string>().Should().Be("sql-dev");
    }

    [Fact]
    public void Interpolates_variables_from_an_env_file()
    {
        WriteFile(".env", "PROJECT_NAME=FromEnvFile");
        WriteFile("aspireform.jsonc", """
            {
              "aspireform": { "version": 1, "project": "${PROJECT_NAME}", "apphost": "./X" }
            }
            """);

        var loaded = _loader.Load(_dir, env: null);

        loaded.Model.AspireForm.Project.Should().Be("FromEnvFile");
    }

    [Fact]
    public void Throws_when_no_config_file_is_found()
    {
        var act = () => _loader.Load(_dir, env: null);
        act.Should().Throw<ConfigValidationException>().WithMessage("*No AspireForm configuration*");
    }

    [Fact]
    public void Throws_when_multiple_base_config_files_are_present()
    {
        WriteFile("aspireform.yaml", "aspireform: { version: 1, project: A, apphost: ./A }");
        WriteFile("aspireform.jsonc", """{ "aspireform": { "version": 1, "project": "A", "apphost": "./A" } }""");

        var act = () => _loader.Load(_dir, env: null);
        act.Should().Throw<ConfigValidationException>().WithMessage("*Multiple*");
    }
}
