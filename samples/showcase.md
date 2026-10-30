# Lumen Markdown — showcase

A local **Markdown** reader and editor for Windows 11, built with WinUI 3 on .NET 10.

## Why it exists

- Open a file from disk
- Read it with real typography
- Edit the source with syntax highlighting
- See the live preview update as you type added by test

> The preview follows the source, not the other way round.

## Features

| Area | v1 |
|------|----|
| Reader | CommonMark + tables, task lists, footnotes |
| Editor | Monospaced source, token highlighting |
| Preview | WebView2, light/dark |
| Files | UTF-8/UTF-16, CRLF/LF preserved |

### Task list

- [x] Render headings
- [x] Render tables
- [ ] Export to HTML (v1.1)

### Code

```csharp
var pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .DisableHtml()
    .Build();
```

Inline code such as `Markdown.ToHtml` is highlighted too, and [links](https://example.com) open in the browser.

---

*Everything stays on this machine.*
