using System.Text.Json;

namespace AspireForm.State;

/// <summary>Reads and writes the <c>.aspireform/state.json</c> file.</summary>
public sealed class StateStore
{
    private const string StateDirName = ".aspireform";
    private const string StateFileName = "state.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Loads state from <paramref name="projectDir"/>, returning a fresh empty state when no file exists.</summary>
    public AspireFormState Load(string projectDir)
    {
        var path = Path.Combine(projectDir, StateDirName, StateFileName);
        if (!File.Exists(path))
        {
            return new AspireFormState();
        }

        try
        {
            return JsonSerializer.Deserialize<AspireFormState>(File.ReadAllText(path), Options)
                ?? new AspireFormState();
        }
        catch (JsonException ex)
        {
            throw new StateException($"The AspireForm state file at '{path}' is corrupt.", ex);
        }
    }

    /// <summary>Writes <paramref name="state"/> to <c>.aspireform/state.json</c> under <paramref name="projectDir"/>.</summary>
    public void Save(string projectDir, AspireFormState state)
    {
        var stateDir = Path.Combine(projectDir, StateDirName);
        Directory.CreateDirectory(stateDir);
        File.WriteAllText(Path.Combine(stateDir, StateFileName), JsonSerializer.Serialize(state, Options));
    }
}
