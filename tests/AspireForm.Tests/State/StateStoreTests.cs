using AspireForm.State;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.State;

public sealed class StateStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aspireform-state-test").FullName;
    private readonly StateStore _store = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Load_returns_empty_state_when_no_state_file_exists()
    {
        var state = _store.Load(_dir);
        state.Version.Should().Be(1);
        state.Blocks.Should().BeEmpty();
    }

    [Fact]
    public void Save_then_Load_round_trips_state()
    {
        var state = new AspireFormState();
        state.Blocks["sql"] = new BlockState
        {
            Type = "sqlserver",
            Kind = "resource",
            Files =
            {
                ["MyApp.AppHost/AppHost.cs"] = new FileState
                {
                    OwnershipMode = "managed",
                    Checksum = "abc123",
                },
            },
        };

        _store.Save(_dir, state);
        var reloaded = _store.Load(_dir);

        reloaded.Blocks.Should().ContainKey("sql");
        reloaded.Blocks["sql"].Type.Should().Be("sqlserver");
        reloaded.Blocks["sql"].Files["MyApp.AppHost/AppHost.cs"].OwnershipMode.Should().Be("managed");
        reloaded.Blocks["sql"].Files["MyApp.AppHost/AppHost.cs"].Checksum.Should().Be("abc123");
    }

    [Fact]
    public void Save_writes_to_the_dot_aspireform_directory()
    {
        _store.Save(_dir, new AspireFormState());
        File.Exists(Path.Combine(_dir, ".aspireform", "state.json")).Should().BeTrue();
    }

    [Fact]
    public void Load_throws_when_the_state_file_is_corrupt()
    {
        var stateDir = Directory.CreateDirectory(Path.Combine(_dir, ".aspireform"));
        File.WriteAllText(Path.Combine(stateDir.FullName, "state.json"), "{ not json");

        var act = () => _store.Load(_dir);
        act.Should().Throw<StateException>();
    }
}
