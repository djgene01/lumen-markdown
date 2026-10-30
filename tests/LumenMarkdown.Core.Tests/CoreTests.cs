using System.Text;
using LumenMarkdown.Core.Documents;
using LumenMarkdown.Core.Editing;
using LumenMarkdown.Core.IO;
using LumenMarkdown.Core.Markdown;
using LumenMarkdown.Core.Settings;
using Xunit;

namespace LumenMarkdown.Core.Tests;

public class EncodingTests
{
    private static string TempFile(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumen-{Guid.NewGuid():N}.md");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public async Task Utf8_NoBom_RoundTrips()
    {
        string path = TempFile(Encoding.UTF8.GetBytes("café\nsecond"));
        try
        {
            var loaded = await TextFileLoader.LoadAsync(path);
            Assert.Equal("café\nsecond", loaded.Text);
            Assert.False(loaded.HasBom);
            Assert.Equal("\n", loaded.LineEnding);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Utf8_Bom_IsStripped()
    {
        string path = TempFile(new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("hello")).ToArray());
        try
        {
            var loaded = await TextFileLoader.LoadAsync(path);
            Assert.Equal("hello", loaded.Text);
            Assert.True(loaded.HasBom);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Crlf_IsDetected()
    {
        string path = TempFile(Encoding.UTF8.GetBytes("a\r\nb\r\n"));
        try
        {
            var loaded = await TextFileLoader.LoadAsync(path);
            Assert.Equal("a\nb\n", loaded.Text);
            Assert.Equal("\r\n", loaded.LineEnding);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Binary_IsRejected()
    {
        string path = TempFile(new byte[] { 0x50, 0x4B, 0x00, 0x01 });
        try
        {
            await Assert.ThrowsAsync<BinaryFileException>(() => TextFileLoader.LoadAsync(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Oversize_IsRejected()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumen-{Guid.NewGuid():N}.md");
        await using (var fs = new FileStream(path, FileMode.Create))
        {
            fs.SetLength(TextFileLoader.MaxFileSize + 1);
        }
        try
        {
            await Assert.ThrowsAsync<FileTooLargeException>(() => TextFileLoader.LoadAsync(path));
        }
        finally { File.Delete(path); }
    }
}

public class WriterTests
{
    [Fact]
    public async Task Save_Preserves_Crlf_And_Encoding()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumen-{Guid.NewGuid():N}.md");
        try
        {
            await TextFileWriter.SaveAsync(path, "a\nb", new UTF8Encoding(false), "\r\n", false);
            byte[] bytes = await File.ReadAllBytesAsync(path);
            Assert.Equal("a\r\nb", Encoding.UTF8.GetString(bytes));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Save_Is_Atomic_And_Overwrites()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumen-{Guid.NewGuid():N}.md");
        try
        {
            await File.WriteAllTextAsync(path, "old");
            await TextFileWriter.SaveAsync(path, "new", new UTF8Encoding(false), "\n", false);
            Assert.Equal("new", await File.ReadAllTextAsync(path));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.*.tmp"));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}

public class DirtyStateTests
{
    [Fact]
    public async Task Undo_To_Original_Is_Clean()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumen-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, "# Title\n\nBody");
        try
        {
            var session = await DocumentSession.LoadAsync(path);
            Assert.False(session.IsDirty);
            session.UpdateText("# Title\n\nChanged");
            Assert.True(session.IsDirty);
            session.UpdateText("# Title\n\nBody");
            Assert.False(session.IsDirty);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Save_Makes_Clean_And_Updates_Path()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumen-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, "x");
        try
        {
            var session = await DocumentSession.LoadAsync(path);
            session.UpdateText("y");
            string other = path + ".new.md";
            session.MarkSaved(other, new UTF8Encoding(false), "\n", false);
            Assert.False(session.IsDirty);
            Assert.Equal(other, session.FilePath);
        }
        finally { File.Delete(path); }
    }
}

public class MarkdownTests
{
    [Fact]
    public void Headings_And_Emphasis_Render()
    {
        string html = MarkdownRenderer.Render("# Title\n\nSome **bold** and *italic*.");
        Assert.Contains("<h1", html);
        Assert.Contains("<strong>bold</strong>", html);
        Assert.Contains("<em>italic</em>", html);
    }

    [Fact]
    public void Tables_And_TaskLists_Render()
    {
        string html = MarkdownRenderer.Render("| a | b |\n|---|---|\n| 1 | 2 |\n\n- [x] done\n- [ ] todo");
        Assert.Contains("<table>", html);
        Assert.Contains("type=\"checkbox\"", html);
    }

    [Fact]
    public void RawHtml_Is_Escaped()
    {
        string html = MarkdownRenderer.Render("<script>alert(1)</script>");
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void FencedCode_Gets_Language_Class()
    {
        string html = MarkdownRenderer.Render("```csharp\nvar x = 1;\n```");
        Assert.Contains("language-csharp", html);
    }

    [Fact]
    public void Shell_Contains_Csp_And_Base()
    {
        string shell = MarkdownRenderer.BuildShell("<p>x</p>", @"C:\docs\a.md", "body{}", "//js", true);
        Assert.Contains("Content-Security-Policy", shell);
        Assert.Contains("data-theme=\"dark\"", shell);
        Assert.Contains("file:///C:/docs/", shell);
    }
}

public class TokenizerTests
{
    [Fact]
    public void Classifies_Heading_And_Fence()
    {
        var tokens = MarkdownTokenizer.Tokenize("# Title\n\n```\ncode\n```");
        Assert.Contains(tokens, t => t.Kind == TokenKind.Heading);
        Assert.True(tokens.Count(t => t.Kind == TokenKind.Fence) >= 2);
    }

    [Fact]
    public void Classifies_List_And_Inline()
    {
        var tokens = MarkdownTokenizer.Tokenize("- item **bold** `code` [link](https://x)");
        Assert.Contains(tokens, t => t.Kind == TokenKind.ListMarker);
        Assert.Contains(tokens, t => t.Kind == TokenKind.Bold);
        Assert.Contains(tokens, t => t.Kind == TokenKind.Code);
        Assert.Contains(tokens, t => t.Kind == TokenKind.Link);
    }

    [Fact]
    public void Empty_Text_Returns_No_Tokens()
    {
        Assert.Empty(MarkdownTokenizer.Tokenize(string.Empty));
    }
}

public class SettingsTests
{
    [Fact]
    public async Task RoundTrip_Preserves_Values()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumen-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(path);
            var settings = new AppSettings { Theme = "Dark", EditorFontSize = 17, SplitterRatio = 0.4 };
            await store.SaveAsync(settings);
            var loaded = store.Load();
            Assert.Equal("Dark", loaded.Theme);
            Assert.Equal(17, loaded.EditorFontSize);
            Assert.Equal(0.4, loaded.SplitterRatio);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Corrupt_Settings_Fall_Back_To_Defaults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumen-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json");
        try
        {
            var loaded = new SettingsStore(path).Load();
            Assert.Equal("System", loaded.Theme);
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(path)!, "settings.bad.json")));
        }
        finally
        {
            File.Delete(path);
            string bad = Path.Combine(Path.GetDirectoryName(path)!, "settings.bad.json");
            if (File.Exists(bad)) File.Delete(bad);
        }
    }

    [Fact]
    public void Recent_Files_Dedupe_And_Cap()
    {
        var list = new List<string>();
        for (int i = 0; i < 12; i++) list = RecentFiles.Add(list, $@"C:\docs\{i}.md");
        Assert.Equal(10, list.Count);
        Assert.Equal(@"C:\docs\11.md", list[0]);
        list = RecentFiles.Add(list, @"C:\docs\5.md");
        Assert.Equal(@"C:\docs\5.md", list[0]);
        Assert.Single(list, p => p == @"C:\docs\5.md");
    }
}
