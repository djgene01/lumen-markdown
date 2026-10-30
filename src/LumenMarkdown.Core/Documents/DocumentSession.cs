using System.Security.Cryptography;
using System.Text;
using LumenMarkdown.Core.IO;

namespace LumenMarkdown.Core.Documents;

/// <summary>Owns the open document's text, path, encoding and dirty state.</summary>
public sealed class DocumentSession
{
    private string _savedHash = Hash(string.Empty);

    private DocumentSession() { }

    public string? FilePath { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public Encoding Encoding { get; private set; } = new UTF8Encoding(false);
    public string LineEnding { get; private set; } = "\r\n";
    public bool HasBom { get; private set; }
    public bool IsReadOnly { get; private set; }
    public bool HadDecodeErrors { get; private set; }
    public bool IsNew => FilePath is null;
    public bool IsDirty => Hash(Text) != _savedHash;
    public string FileName => FilePath is null ? "Untitled.md" : Path.GetFileName(FilePath);

    public event EventHandler? Changed;

    public static async Task<DocumentSession> LoadAsync(string path, CancellationToken ct = default)
    {
        var loaded = await TextFileLoader.LoadAsync(path, ct).ConfigureAwait(false);
        return new DocumentSession
        {
            FilePath = Path.GetFullPath(path),
            Text = loaded.Text,
            Encoding = loaded.Encoding,
            LineEnding = loaded.LineEnding,
            HasBom = loaded.HasBom,
            IsReadOnly = loaded.IsReadOnly,
            HadDecodeErrors = loaded.HadDecodeErrors,
            _savedHash = Hash(loaded.Text)
        };
    }

    public static DocumentSession CreateUntitled()
    {
        var session = new DocumentSession
        {
            Text = string.Empty,
            Encoding = new UTF8Encoding(false),
            LineEnding = "\r\n",
            HasBom = false
        };
        session._savedHash = Hash(string.Empty);
        return session;
    }

    public void UpdateText(string text)
    {
        if (text == Text) return;
        Text = text;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkSaved(string path, Encoding encoding, string lineEnding, bool hasBom)
    {
        FilePath = Path.GetFullPath(path);
        Encoding = encoding;
        LineEnding = lineEnding;
        HasBom = hasBom;
        IsReadOnly = false;
        HadDecodeErrors = false;
        _savedHash = Hash(Text);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Hash(string text)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}
