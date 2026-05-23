using System.Text.Json.Nodes;
using AspireForm.Providers;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Providers;

public sealed class EfDataModuleProviderTests
{
    private readonly EfDataModuleProvider _provider = new();

    private static PlanContext Ctx(JsonObject inputs) =>
        new("data", inputs, AppHostDirectory: "./MyApp.AppHost", ProjectName: "MyApp");

    [Fact]
    public void Type_and_kind_are_correct()
    {
        _provider.Type.Should().Be("ef-data");
        _provider.Kind.Should().Be(BlockKind.Module);
    }

    [Fact]
    public void Plan_scaffolds_the_dbcontext_class_at_the_configured_path()
    {
        var inputs = new JsonObject
        {
            ["database"] = "appdb",
            ["contextName"] = "AppDbContext",
        };

        var plan = _provider.Plan(Ctx(inputs));

        var scaffoldFile = plan.FileActions
            .SingleOrDefault(f => f.OwnershipMode == OwnershipMode.Scaffold);
        scaffoldFile.Should().NotBeNull();
        scaffoldFile!.Path.Replace('\\', '/').Should().Be("./MyApp.AppHost/Data/AppDbContext.cs");
        scaffoldFile.RenderContent().Should().Contain("class AppDbContext : DbContext");
        scaffoldFile.RenderContent().Should().Contain("Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void Plan_emits_a_managed_apphost_region_recording_the_database_dependency()
    {
        var inputs = new JsonObject
        {
            ["database"] = "appdb",
            ["contextName"] = "AppDbContext",
        };

        var plan = _provider.Plan(Ctx(inputs));

        var managedFile = plan.FileActions
            .SingleOrDefault(f => f.OwnershipMode == OwnershipMode.Managed);
        managedFile.Should().NotBeNull();
        managedFile!.BlockMarker.Should().Be("data");

        var content = managedFile.RenderContent();
        content.Should().Contain("ef-data module").And.Contain("AppDbContext").And.Contain("appdb");
    }

    [Fact]
    public void Plan_emits_no_cli_actions_in_v1()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["database"] = "appdb", ["contextName"] = "X" }));
        plan.CliActions.Should().BeEmpty();
    }

    [Fact]
    public void Plan_defaults_contextName_when_absent()
    {
        var plan = _provider.Plan(Ctx(new JsonObject { ["database"] = "appdb" }));
        var scaffold = plan.FileActions.Single(f => f.OwnershipMode == OwnershipMode.Scaffold);
        scaffold.Path.Should().EndWith("AppDbContext.cs");
    }
}
