using System.Text;

namespace LumenMarkdown.Core.IO;

/// <summary>Writes text atomically, preserving the requested encoding and line ending.</summary>
public static class TextFileWriter
{
    public static async Task SaveAsync(
        string path,
        string text,
        Encoding encoding,
        string lineEnding,
        bool writeBom,
        CancellationToken ct = default)
    {
        string normalized = NormalizeNewlines(text, lineEnding);
        byte[] preamble = writeBom ? encoding.GetPreamble() : [];
        byte[] body = encoding.GetBytes(normalized);
        byte[] payload = new byte[preamble.Length + body.Length];
        preamble.CopyTo(payload, 0);
        body.CopyTo(payload, preamble.Length);

        string directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        Directory.CreateDirectory(directory);
        string temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temp, payload, ct).ConfigureAwait(false);
            if (File.Exists(path))
            {
                var attrs = File.GetAttributes(path);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    throw new UnauthorizedAccessException($"'{Path.GetFileName(path)}' is read-only.");
                File.Replace(temp, path, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, path);
            }
        }
        finally
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); } catch { /* best effort */ }
            }
        }
    }

    public static string NormalizeNewlines(string text, string lineEnding) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", lineEnding);
}
