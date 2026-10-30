# Lumen Markdown

A local Markdown reader and editor for Windows 11, built with **WinUI 3** on **.NET 10**.

- Open `.md`, `.markdown` and `.txt` files from disk, drag-and-drop, or the command line.
- Read CommonMark with tables, task lists, footnotes and YAML front-matter.
- Edit the source with Markdown syntax highlighting, line numbers, find and go-to-line.
- See a live HTML preview in WebView2, side by side, with light and dark themes.
- Save with encoding and line endings preserved; recent files and window state persist as JSON.

## Requirements

- Windows 11 (x64)
- .NET 10 Desktop Runtime (x64)
- Microsoft Edge WebView2 Runtime (present on Windows 11 by default)

## Build

```powershell
dotnet build src/LumenMarkdown.App/LumenMarkdown.App.csproj -c Release -p:Platform=x64
dotnet test tests/LumenMarkdown.Core.Tests -c Release
```

`LumenMarkdown.slnx` opens in Visual Studio, but the solution-level build does not
carry the x64 platform mapping for this project — build the app project directly.

## Run

```powershell
dotnet run --project src/LumenMarkdown.App -p:Platform=x64 -- samples/showcase.md
```

## Publish the portable ZIP

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish.ps1
```

## Build the MSI

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-installer.ps1
```

The per-machine MSI installs to `%ProgramFiles%\Lumen Markdown`, adds a Start Menu
shortcut and registers in Installed apps. Installing needs an elevated prompt. See
`docs/installer.md`.

## Layout

- `src/LumenMarkdown.Core` — documents, encoding, Markdig rendering, settings, tokenizer (no UI dependency).
- `src/LumenMarkdown.App` — WinUI 3 shell, editor, WebView2 preview, services.
- `tests/LumenMarkdown.Core.Tests` — 20 unit/integration tests.
- `Development-Plans/` — the planning manuals and diagrams this implementation follows.
- `docs/` — acceptance run, performance measurements, implementation status.
- `Packaging/` — text shipped inside the ZIP; `scripts/publish.ps1` builds it.

## Known limitations (v1)

- One document per window; no tabs.
- Raw HTML inside Markdown is escaped, not rendered.
- Syntax highlighting is skipped above roughly 4 000 tokens (very large files) to keep typing responsive.
- Unsigned build: SmartScreen may warn on first run.

## Licence

MIT — see [LICENSE](LICENSE).
