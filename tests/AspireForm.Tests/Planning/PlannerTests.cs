using System.Text.Json.Nodes;
using AspireForm.Configuration;
using AspireForm.Planning;
using AspireForm.Providers;
using AspireForm.State;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Planning;

public sealed class PlannerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-planner").FullName;
    private readonly Planner _planner = new(ProviderRegistry.Default());

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static ProjectModel ModelWith(params (string Name, ResourceBlock Block)[] resources)
    {
        var dict = resources.ToDictionary(r => r.Name, r => r.Block);
        return new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "MyApp", AppHost = "./MyApp.AppHost" },
            Resources = dict,
        };
    }

    [Fact]
    public void Plan_for_a_new_sql_resource_emits_a_create_block_action()
    {
        var model = ModelWith(("sql", new ResourceBlock
        {
            Name = "sql",
            Type = "sqlserver",
            Inputs = new JsonObject { ["aspireName"] = "sql" },
        }));

        var plan = _planner.Plan(model, new AspireFormState(), _dir);

        plan.Blocks.Should().ContainSingle();
        plan.Blocks[0].BlockName.Should().Be("sql");
        plan.Blocks[0].Kind.Should().Be(BlockActionKind.Create);
        plan.HasChanges.Should().BeTrue();
    }

    [Fact]
    public void Plan_orders_modules_after_their_resource_dependencies()
    {
        var model = new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "MyApp", AppHost = "./MyApp.AppHost" },
            Resources = new Dictionary<string, ResourceBlock>
            {
                ["sql"] = new() { Name = "sql", Type = "sqlserver", Inputs = new JsonObject() },
            },
            Modules = new Dictionary<string, ModuleBlock>
            {
                ["data"] = new()
                {
                    Name = "data", Type = "ef-data", DependsOn = ["sql"],
                    Inputs = new JsonObject { ["database"] = "appdb", ["contextName"] = "AppDbContext" },
                },
            },
        };

        var plan = _planner.Plan(model, new AspireFormState(), _dir);

        plan.Blocks.Select(b => b.BlockName).Should().Equal("sql", "data");
    }

    [Fact]
    public void Plan_proposes_delete_for_a_block_in_state_but_absent_from_config()
    {
        var state = new AspireFormState();
        state.Blocks["sql"] = new BlockState
        {
            Type = "sqlserver", Kind = "resource",
            Files = { [Path.Combine(_dir, "AppHost.cs")] =
                new FileState { OwnershipMode = "managed", Checksum = "x" } },
        };

        var plan = _planner.Plan(ModelWith(), state, _dir);

        plan.Blocks.Should().ContainSingle(b => b.BlockName == "sql" && b.Kind == BlockActionKind.Delete);
    }

    [Fact]
    public void Plan_throws_when_a_provider_type_is_unknown()
    {
        var model = ModelWith(("x", new ResourceBlock { Name = "x", Type = "no-such-provider", Inputs = new JsonObject() }));
        var act = () => _planner.Plan(model, new AspireFormState(), _dir);
        act.Should().Throw<ProviderNotFoundException>();
    }

    [Fact]
    public void Plan_throws_on_a_dependency_cycle()
    {
        var model = new ProjectModel
        {
            AspireForm = new AspireFormHeader { Version = 1, Project = "MyApp", AppHost = "./MyApp.AppHost" },
            Modules = new Dictionary<string, ModuleBlock>
            {
                ["a"] = new() { Name = "a", Type = "ef-data", DependsOn = ["b"], Inputs = new JsonObject() },
                ["b"] = new() { Name = "b", Type = "ef-data", DependsOn = ["a"], Inputs = new JsonObject() },
            },
        };

        var act = () => _planner.Plan(model, new AspireFormState(), _dir);
        act.Should().Throw<DependencyCycleException>();
    }
}
