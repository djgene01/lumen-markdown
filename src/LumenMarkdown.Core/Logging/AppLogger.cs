using System.Text;

namespace LumenMarkdown.Core.Logging;

/// <summary>Minimal rolling file logger. Never logs document content.</summary>
public static class AppLogger
{
    private static readonly object Gate = new();
    private static string? _directory;

    public static string? LogDirectory => _directory;

    public static void Initialize(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
        Prune(directory, TimeSpan.FromDays(7));
    }

    public static void Info(string message) => Write("INFO", message, null);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        if (_directory is null) return;
        try
        {
            string path = Path.Combine(_directory, $"app-{DateTime.Now:yyyyMMdd}.log");
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
              .Append(" [").Append(level).Append("] ").Append(message);
            if (ex is not null) sb.AppendLine().Append(ex);
            lock (Gate)
            {
                File.AppendAllText(path, sb.AppendLine().ToString());
            }
        }
        catch
        {
            // Logging must never take the app down.
        }
    }

    private static void Prune(string directory, TimeSpan maxAge)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "app-*.log"))
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > maxAge) File.Delete(file);
            }
        }
        catch
        {
            // best effort
        }
    }
}
