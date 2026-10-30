using System.Text;

namespace LumenMarkdown.Core.IO;

/// <summary>Loads text files with encoding, BOM, binary and size detection.</summary>
public static class TextFileLoader
{
    public const long MaxFileSize = 10L * 1024 * 1024;

    public static async Task<LoadedFile> LoadAsync(string path, CancellationToken ct = default)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("File not found.", path);
        if (info.Length > MaxFileSize) throw new FileTooLargeException(info.Length, MaxFileSize);

        byte[] bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        if (LooksBinary(bytes)) throw new BinaryFileException();

        var (encoding, hasBom, offset) = DetectEncoding(bytes);
        bool hadErrors = false;
        string text;
        try
        {
            var strict = (Encoding)encoding.Clone();
            strict.DecoderFallback = DecoderFallback.ExceptionFallback;
            text = strict.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            var lenient = (Encoding)encoding.Clone();
            lenient.DecoderFallback = DecoderFallback.ReplacementFallback;
            text = lenient.GetString(bytes, offset, bytes.Length - offset);
            hadErrors = true;
        }

        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lineEnding = DetectLineEnding(bytes);
        var isReadOnly = info.IsReadOnly;
        return new LoadedFile(text, encoding, hasBom, lineEnding, isReadOnly, info.Length, hadErrors);
    }

    private static bool LooksBinary(byte[] bytes)
    {
        int scan = Math.Min(bytes.Length, 8192);
        for (int i = 0; i < scan; i++)
        {
            if (bytes[i] == 0) return true;
        }
        return false;
    }

    private static (Encoding Encoding, bool HasBom, int Offset) DetectEncoding(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
            return (new UTF8Encoding(true), true, 3);
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE)
            return (new UnicodeEncoding(false, true), true, 2);
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF)
            return (new UnicodeEncoding(true, true), true, 2);
        return (new UTF8Encoding(false), false, 0);
    }

    private static string DetectLineEnding(byte[] bytes)
    {
        int crlf = 0, lf = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte)'\n')
            {
                if (i > 0 && bytes[i - 1] == (byte)'\r') crlf++;
                else lf++;
            }
        }
        if (crlf == 0 && lf == 0) return Environment.NewLine;
        return crlf >= lf ? "\r\n" : "\n";
    }
}
