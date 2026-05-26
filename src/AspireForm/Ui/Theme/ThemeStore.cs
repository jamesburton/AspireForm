using System.Text.Json;
using System.Text.Json.Nodes;

namespace AspireForm.Ui.Theme;

/// <summary>File-backed implementation of <see cref="IThemeStore"/>.
/// Persists overrides to <c>.aspireform/theme.json</c> in the project directory.</summary>
internal sealed class ThemeStore : IThemeStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, string>? _overrides; // null = not yet loaded

    /// <summary>Initialises the store for the given project directory.</summary>
    public ThemeStore(string projectDir)
    {
        _filePath = Path.Combine(projectDir, ".aspireform", "theme.json");
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> GetTokens()
    {
        EnsureLoaded();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        // Start with defaults.
        foreach (var token in ThemeDefaults.Tokens)
            result[token.Name] = token.DefaultValue;

        // Apply persisted overrides.
        foreach (var kv in _overrides!)
            result[kv.Key] = kv.Value;

        return result;
    }

    /// <inheritdoc/>
    public async Task SaveTokenAsync(string name, string value, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            EnsureLoaded();
            _overrides![name] = value;
            await WriteAsync(ct);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task ResetToDefaultsAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _overrides = [];
            await WriteAsync(ct);
        }
        finally { _lock.Release(); }
    }

    private void EnsureLoaded()
    {
        if (_overrides is not null) return;
        _overrides = [];
        if (!File.Exists(_filePath)) return;
        try
        {
            var json = File.ReadAllText(_filePath);
            var node = JsonNode.Parse(json);
            if (node is JsonObject obj)
            {
                foreach (var kv in obj)
                {
                    if (kv.Value?.GetValueKind() == JsonValueKind.String)
                        _overrides[kv.Key] = kv.Value.GetValue<string>();
                }
            }
        }
        catch (JsonException)
        {
            // Malformed file — treat as empty overrides, don't crash.
            _overrides = [];
        }
    }

    private async Task WriteAsync(CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);
        var obj = new JsonObject();
        foreach (var kv in _overrides!)
            obj[kv.Key] = JsonValue.Create(kv.Value);
        var json = obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json, ct);
    }
}
