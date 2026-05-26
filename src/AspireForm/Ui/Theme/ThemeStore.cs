using System.Text.Json;
using System.Text.Json.Nodes;

namespace AspireForm.Ui.Theme;

/// <summary>File-backed multi-theme store. Themes are stored as individual JSON files under
/// <c>{projectDir}/.aspireform/themes/</c>. The active pointer lives at <c>_active.json</c>.</summary>
internal sealed class ThemeStore : IThemeStore
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private readonly string _themesDir;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Initialises the store for the given project directory.</summary>
    public ThemeStore(string projectDir)
    {
        _themesDir = Path.Combine(projectDir, ".aspireform", "themes");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ThemeSummary>> ListAsync(CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var activation = await ReadManifestAsync(ct);
        var files = Directory.GetFiles(_themesDir, "*.json")
            .Where(f => !Path.GetFileName(f).Equals("_active.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var summaries = new List<ThemeSummary>(files.Count);
        foreach (var f in files)
        {
            try
            {
                var def = await ReadThemeFileAsync(f, ct);
                summaries.Add(new ThemeSummary(def.Name, def.Description,
                    string.Equals(def.Name, activation.Active, StringComparison.OrdinalIgnoreCase)));
            }
            catch (ThemeLoadException)
            {
                // Surface broken themes as disabled entries (name from filename).
                var broken = Path.GetFileNameWithoutExtension(f);
                summaries.Add(new ThemeSummary(broken, "(malformed)", false));
            }
        }
        return summaries;
    }

    /// <inheritdoc/>
    public async Task<ThemeDefinition> GetAsync(string name, CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var path = ThemePath(name);
        if (!File.Exists(path))
            throw new ThemeLoadException($"Theme '{name}' not found at '{path}'.");
        return await ReadThemeFileAsync(path, ct);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(ThemeDefinition theme, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_themesDir);
        await _lock.WaitAsync(ct);
        try
        {
            var json = JsonSerializer.Serialize(theme, JsonOpts);
            await File.WriteAllTextAsync(ThemePath(theme.Name), json, ct);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string name, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var path = ThemePath(name);
            if (File.Exists(path)) File.Delete(path);

            var manifest = await ReadManifestAsync(ct);
            if (string.Equals(manifest.Active, name, StringComparison.OrdinalIgnoreCase))
            {
                // Fall back to first available theme.
                var remaining = Directory.GetFiles(_themesDir, "*.json")
                    .Where(f => !Path.GetFileName(f).Equals("_active.json", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (remaining is not null)
                {
                    var fallback = await ReadThemeFileAsync(remaining, ct);
                    await WriteManifestAsync(new ThemeManifest { Active = fallback.Name, DarkMode = manifest.DarkMode }, ct);
                }
            }
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task<string> DuplicateAsync(string sourceName, string newName, CancellationToken ct = default)
    {
        var source = await GetAsync(sourceName, ct);
        var copy = source with { Name = newName, Description = $"Copy of {source.Description}" };
        await SaveAsync(copy, ct);
        return newName;
    }

    /// <inheritdoc/>
    public async Task RenameAsync(string oldName, string newName, CancellationToken ct = default)
    {
        var source = await GetAsync(oldName, ct);
        var renamed = source with { Name = newName };
        await SaveAsync(renamed, ct);

        var oldPath = ThemePath(oldName);
        if (File.Exists(oldPath)) File.Delete(oldPath);

        var manifest = await ReadManifestAsync(ct);
        if (string.Equals(manifest.Active, oldName, StringComparison.OrdinalIgnoreCase))
            await WriteManifestAsync(new ThemeManifest { Active = newName, DarkMode = manifest.DarkMode }, ct);
    }

    /// <inheritdoc/>
    public async Task<ThemeActivation> GetActiveAsync(CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var manifest = await ReadManifestAsync(ct);
        return new ThemeActivation(manifest.Active, manifest.DarkMode);
    }

    /// <inheritdoc/>
    public async Task SetActiveAsync(string name, CancellationToken ct = default)
    {
        if (!File.Exists(ThemePath(name)))
            throw new ThemeLoadException($"Theme '{name}' does not exist.");
        var current = await ReadManifestAsync(ct);
        await WriteManifestAsync(new ThemeManifest { Active = name, DarkMode = current.DarkMode }, ct);
    }

    /// <inheritdoc/>
    public async Task SetDarkModeAsync(bool dark, CancellationToken ct = default)
    {
        var current = await ReadManifestAsync(ct);
        await WriteManifestAsync(new ThemeManifest { Active = current.Active, DarkMode = dark }, ct);
    }

    /// <inheritdoc/>
    public async Task ResetToDefaultsAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_themesDir);
        foreach (var theme in ThemeDefaults.BuiltIn())
            await SaveAsync(theme, ct);
        await WriteManifestAsync(new ThemeManifest { Active = "AspireForm Light", DarkMode = false }, ct);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task EnsureDefaultsAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_themesDir);

        /* Check if any non-manifest theme files exist. */
        var hasThemes = Directory.GetFiles(_themesDir, "*.json")
            .Any(f => !Path.GetFileName(f).Equals("_active.json", StringComparison.OrdinalIgnoreCase));

        if (!hasThemes)
        {
            await MigrateLegacyIfExistsAsync(ct);
            foreach (var theme in ThemeDefaults.BuiltIn())
                await SaveAsync(theme, ct);
            if (!File.Exists(ActivePath()))
                await WriteManifestAsync(new ThemeManifest { Active = "AspireForm Light" }, ct);
        }
    }

    private async Task MigrateLegacyIfExistsAsync(CancellationToken ct)
    {
        /* Migrate v0.7 single-file theme.json → "Migrated v0.7" theme. */
        var legacyPath = Path.Combine(Path.GetDirectoryName(_themesDir)!, "theme.json");
        if (!File.Exists(legacyPath)) return;
        try
        {
            var json = await File.ReadAllTextAsync(legacyPath, ct);
            var node = JsonNode.Parse(json);
            if (node is not JsonObject obj) return;
            var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in obj)
            {
                if (kv.Value?.GetValueKind() == System.Text.Json.JsonValueKind.String)
                    tokens[kv.Key] = kv.Value.GetValue<string>();
            }
            if (tokens.Count > 0)
            {
                /* Best-effort: use the legacy tokens as both light and dark. */
                var migrated = new ThemeDefinition("Migrated v0.7", "Imported from legacy theme.json", tokens, tokens, 0.5);
                await SaveAsync(migrated, ct);
                await WriteManifestAsync(new ThemeManifest { Active = "Migrated v0.7" }, ct);
            }
        }
        catch
        {
            /* Legacy migration is best-effort — don't block startup. */
        }
    }

    private string ThemePath(string name)
    {
        /* Sanitize: replace spaces with hyphens, lowercase, keep alphanumeric/- */
        var slug = name.ToLowerInvariant().Replace(' ', '-');
        slug = new string(slug.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-').ToArray());
        return Path.Combine(_themesDir, $"{slug}.json");
    }

    private string ActivePath() => Path.Combine(_themesDir, "_active.json");

    private async Task<ThemeManifest> ReadManifestAsync(CancellationToken ct)
    {
        var path = ActivePath();
        if (!File.Exists(path)) return new ThemeManifest();
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<ThemeManifest>(json, JsonOpts) ?? new ThemeManifest();
        }
        catch { return new ThemeManifest(); }
    }

    private async Task WriteManifestAsync(ThemeManifest manifest, CancellationToken ct)
    {
        Directory.CreateDirectory(_themesDir);
        var json = JsonSerializer.Serialize(manifest, JsonOpts);
        await File.WriteAllTextAsync(ActivePath(), json, ct);
    }

    private static async Task<ThemeDefinition> ReadThemeFileAsync(string path, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            var def = JsonSerializer.Deserialize<ThemeDefinition>(json, JsonOpts);
            if (def is null)
                throw new ThemeLoadException($"Null deserialization result from '{path}'.");
            return def;
        }
        catch (ThemeLoadException) { throw; }
        catch (Exception ex)
        {
            throw new ThemeLoadException($"Failed to load theme from '{path}': {ex.Message}", ex);
        }
    }
}
