using System.Text;
using Markdig;
using Md = Markdig.Markdown;

namespace LumenMarkdown.Core.Markdown;

/// <summary>Turns Markdown into an HTML fragment. Pure and thread-safe.</summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseYamlFrontMatter()
        .DisableHtml()
        .Build();

    public static string Render(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return "<p class=\"empty\">This document is empty.</p>";
        return Md.ToHtml(markdown, Pipeline);
    }

    /// <summary>Builds the complete preview document, with CSS and bridge script inlined.</summary>
    public static string BuildShell(string fragment, string? documentPath, string css, string js, bool dark)
    {
        string baseUri = string.Empty;
        if (!string.IsNullOrWhiteSpace(documentPath))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(documentPath));
            if (!string.IsNullOrEmpty(dir))
            {
                var normalized = dir.Replace('\\', '/');
                if (!normalized.EndsWith('/')) normalized += "/";
                baseUri = $"<base href=\"file:///{normalized}\">";
            }
        }

        string theme = dark ? "dark" : "light";
        var html = new StringBuilder(fragment.Length + css.Length + js.Length + 2048);
        html.AppendLine("<!doctype html>");
        html.AppendLine($"<html data-theme=\"{theme}\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src file: data:; style-src 'unsafe-inline'; font-src file:; script-src 'unsafe-inline'\">");
        html.AppendLine(baseUri);
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine("<style>");
        html.AppendLine(css);
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<article id=\"content\">");
        html.AppendLine(fragment);
        html.AppendLine("</article>");
        html.AppendLine("<script>");
        html.AppendLine(js);
        html.AppendLine("</script>");
        html.AppendLine("</body></html>");
        return html.ToString();
    }
}
