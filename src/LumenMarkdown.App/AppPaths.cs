namespace LumenMarkdown.App;

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LumenMarkdown");

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string LogDirectory => Path.Combine(Root, "logs");
    public static string DraftDirectory => Path.Combine(Root, "drafts");
    public static string AssetDirectory => Path.Combine(AppContext.BaseDirectory, "Assets");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(DraftDirectory);
    }
}
