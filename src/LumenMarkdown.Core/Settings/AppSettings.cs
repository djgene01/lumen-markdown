using System.Text.Json;
using System.Text.Json.Serialization;

namespace LumenMarkdown.Core.Settings;

public sealed class WindowSettings
{
    public int X { get; set; } = 120;
    public int Y { get; set; } = 80;
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 820;
    public bool Maximized { get; set; }
}

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public string Theme { get; set; } = "System";
    public double EditorFontSize { get; set; } = 14;
    public double PreviewZoom { get; set; } = 1.0;
    public string Layout { get; set; } = "Split";
    public double SplitterRatio { get; set; } = 0.5;
    public bool WordWrap { get; set; } = false;
    public bool ShowLineNumbers { get; set; } = true;
    public WindowSettings Window { get; set; } = new();
    public List<string> RecentFiles { get; set; } = new();
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettings))]
public partial class SettingsJsonContext : JsonSerializerContext;
