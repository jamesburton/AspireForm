using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Configuration;

public sealed class ProjectModelTests
{
    [Fact]
    public void ResourceBlock_defaults_inputs_to_empty_object()
    {
        var block = new ResourceBlock { Name = "sql", Type = "sqlserver" };
        block.Inputs.Should().NotBeNull();
        block.Inputs.Count.Should().Be(0);
    }

    [Fact]
    public void ModuleBlock_is_destroy_protected_by_default()
    {
        var block = new ModuleBlock { Name = "data", Type = "ef-data" };
        block.PreventDestroy.Should().BeTrue();
        block.DependsOn.Should().BeEmpty();
    }

    [Fact]
    public void ProjectModel_holds_header_resources_and_modules()
    {
        var model = new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "MyApp", AppHost = "./MyApp.AppHost" },
            Resources = new Dictionary<string, ResourceBlock>
            {
                ["sql"] = new() { Name = "sql", Type = "sqlserver", Inputs = new JsonObject() },
            },
        };

        model.AspireForm.Project.Should().Be("MyApp");
        model.Resources.Should().ContainKey("sql");
        model.Modules.Should().BeEmpty();
    }
}
