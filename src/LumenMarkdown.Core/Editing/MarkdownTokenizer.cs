using System.Text.RegularExpressions;

namespace LumenMarkdown.Core.Editing;

public enum TokenKind
{
    Heading,
    Bold,
    Italic,
    Code,
    Link,
    ListMarker,
    Quote,
    Fence
}

public readonly record struct TokenSpan(int Start, int Length, TokenKind Kind);

/// <summary>Classifies Markdown source into colouring spans. Line-based, forgiving.</summary>
public static partial class MarkdownTokenizer
{
    [GeneratedRegex(@"^(\s*)(#{1,6})(\s.*)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(\s*)([-*+]|\d{1,9}[.)])(\s)")]
    private static partial Regex ListRegex();

    [GeneratedRegex(@"`[^`\r\n]+`")]
    private static partial Regex CodeSpanRegex();

    [GeneratedRegex(@"\*\*[^*\r\n]+\*\*|__[^_\r\n]+__")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"(?<!\*)\*[^*\r\n]+\*(?!\*)|(?<!_)_[^_\r\n]+_(?!_)")]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"\[[^\]\r\n]*\]\([^)\r\n]*\)")]
    private static partial Regex LinkRegex();

    public static IReadOnlyList<TokenSpan> Tokenize(string text)
    {
        var tokens = new List<TokenSpan>();
        if (string.IsNullOrEmpty(text)) return tokens;

        int lineStart = 0;
        bool inFence = false;

        while (lineStart <= text.Length)
        {
            int lineEnd = text.IndexOf('\n', lineStart);
            if (lineEnd < 0) lineEnd = text.Length;
            int length = lineEnd - lineStart;
            string line = length > 0 ? text.Substring(lineStart, length) : string.Empty;
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                tokens.Add(new TokenSpan(lineStart, length, TokenKind.Fence));
                inFence = !inFence;
            }
            else if (inFence)
            {
                if (length > 0) tokens.Add(new TokenSpan(lineStart, length, TokenKind.Fence));
            }
            else if (HeadingRegex().IsMatch(line))
            {
                tokens.Add(new TokenSpan(lineStart, length, TokenKind.Heading));
            }
            else if (trimmed.StartsWith('>'))
            {
                tokens.Add(new TokenSpan(lineStart + (line.Length - trimmed.Length), trimmed.Length, TokenKind.Quote));
            }
            else
            {
                var list = ListRegex().Match(line);
                if (list.Success)
                {
                    tokens.Add(new TokenSpan(lineStart + list.Groups[1].Length, list.Groups[2].Length, TokenKind.ListMarker));
                }

                AddInline(tokens, lineStart, line, CodeSpanRegex(), TokenKind.Code);
                AddInline(tokens, lineStart, line, BoldRegex(), TokenKind.Bold);
                AddInline(tokens, lineStart, line, LinkRegex(), TokenKind.Link);
                AddInline(tokens, lineStart, line, ItalicRegex(), TokenKind.Italic);
            }

            if (lineEnd >= text.Length) break;
            lineStart = lineEnd + 1;
        }

        return tokens;
    }

    private static void AddInline(List<TokenSpan> tokens, int lineStart, string line, Regex regex, TokenKind kind)
    {
        foreach (Match match in regex.Matches(line))
        {
            if (match.Length == 0) continue;
            tokens.Add(new TokenSpan(lineStart + match.Index, match.Length, kind));
        }
    }
}
