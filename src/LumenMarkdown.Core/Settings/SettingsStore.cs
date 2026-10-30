using System.Text.Json;

namespace LumenMarkdown.Core.Settings;

/// <summary>Loads and atomically saves the JSON settings file.</summary>
public sealed class SettingsStore(string filePath)
{
    public string FilePath { get; } = filePath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            string json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
            if (settings is null) throw new JsonException("Settings deserialized to null.");
            if (settings.SchemaVersion > 1) throw new JsonException($"Unknown schema version {settings.SchemaVersion}.");
            settings.RecentFiles = RecentFiles.Prune(settings.RecentFiles);
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            TryBackup();
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        string? dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
        string temp = FilePath + ".tmp";
        await File.WriteAllTextAsync(temp, json, ct).ConfigureAwait(false);
        File.Move(temp, FilePath, overwrite: true);
    }

    private void TryBackup()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string bad = Path.Combine(Path.GetDirectoryName(FilePath) ?? ".", "settings.bad.json");
                File.Copy(FilePath, bad, overwrite: true);
            }
        }
        catch
        {
            // Diagnostics must never block startup.
        }
    }
}

public static class RecentFiles
{
    public const int MaxItems = 10;

    public static List<string> Add(IEnumerable<string> existing, string path)
    {
        var list = existing.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        if (list.Count > MaxItems) list.RemoveRange(MaxItems, list.Count - MaxItems);
        return list;
    }

    public static List<string> Prune(IEnumerable<string>? paths) =>
        (paths ?? []).Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)).Take(MaxItems).ToList();
}
