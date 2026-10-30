using LumenMarkdown.Core.Editing;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace LumenMarkdown.App.Services;

/// <summary>Applies Markdown token colours to a RichEditBox without disturbing the selection.</summary>
public static class EditorHighlighter
{
    public static void Apply(RichEditBox editor, IReadOnlyList<TokenSpan> tokens, bool dark)
    {
        var document = editor.Document;
        document.GetText(TextGetOptions.None, out string text);
        if (string.IsNullOrEmpty(text)) return;

        int length = Math.Max(0, text.Length - 1);
        if (length == 0) return;

        int selStart = document.Selection.StartPosition;
        int selEnd = document.Selection.EndPosition;

        Color foreground = dark ? Color.FromArgb(255, 230, 230, 234) : Color.FromArgb(255, 27, 27, 31);
        var whole = document.GetRange(0, length);
        whole.CharacterFormat.ForegroundColor = foreground;

        if (tokens.Count <= 4000)
        {
            foreach (var token in tokens)
            {
                int start = Math.Clamp(token.Start, 0, length);
                int end = Math.Clamp(token.Start + token.Length, 0, length);
                if (end <= start) continue;

                var range = document.GetRange(start, end);
                switch (token.Kind)
                {
                    case TokenKind.Heading:
                        range.CharacterFormat.ForegroundColor = dark ? Color.FromArgb(255, 124, 179, 236) : Color.FromArgb(255, 32, 86, 145);
                        range.CharacterFormat.Bold = FormatEffect.On;
                        break;
                    case TokenKind.Code:
                    case TokenKind.Fence:
                        range.CharacterFormat.ForegroundColor = dark ? Color.FromArgb(255, 126, 206, 180) : Color.FromArgb(255, 15, 110, 90);
                        range.CharacterFormat.Name = "Cascadia Mono";
                        break;
                    case TokenKind.Link:
                        range.CharacterFormat.ForegroundColor = dark ? Color.FromArgb(255, 140, 190, 245) : Color.FromArgb(255, 20, 90, 175);
                        break;
                    case TokenKind.Bold:
                        range.CharacterFormat.Bold = FormatEffect.On;
                        range.CharacterFormat.ForegroundColor = dark ? Color.FromArgb(255, 240, 180, 120) : Color.FromArgb(255, 150, 85, 15);
                        break;
                    case TokenKind.Italic:
                        range.CharacterFormat.Italic = FormatEffect.On;
                        range.CharacterFormat.ForegroundColor = dark ? Color.FromArgb(255, 240, 180, 120) : Color.FromArgb(255, 150, 85, 15);
                        break;
                    case TokenKind.ListMarker:
                        range.CharacterFormat.ForegroundColor = dark ? Color.FromArgb(255, 160, 165, 175) : Color.FromArgb(255, 100, 105, 115);
                        break;
                    case TokenKind.Quote:
                        range.CharacterFormat.ForegroundColor = dark ? Color.FromArgb(255, 170, 175, 185) : Color.FromArgb(255, 95, 100, 110);
                        range.CharacterFormat.Italic = FormatEffect.On;
                        break;
                }
            }
        }

        document.Selection.SetRange(selStart, selEnd);
    }

    public static void ApplyDefaultStyle(RichEditBox editor, bool dark, double fontSize)
    {
        var document = editor.Document;
        document.GetText(TextGetOptions.None, out string text);
        int length = Math.Max(0, text.Length - 1);
        if (length <= 0) return;
        var range = document.GetRange(0, length);
        range.CharacterFormat.Name = "Cascadia Mono";
        range.CharacterFormat.Size = (float)fontSize;
        range.CharacterFormat.ForegroundColor = dark ? Color.FromArgb(255, 230, 230, 234) : Color.FromArgb(255, 27, 27, 31);
    }
}
