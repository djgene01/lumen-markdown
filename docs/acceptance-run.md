# Acceptance run — 30 October 2026

Executed against the Release build and the extracted portable ZIP on Windows 11 x64.
Each row records what was actually observed, not what the code is intended to do.

| # | Check | Method | Result |
|---|-------|--------|--------|
| 1 | Launch to interactive window | Published EXE launched with a file argument | Pass — window active in 336 ms, editor and preview populated |
| 2 | Markdown rendering | `samples/showcase.md` rendered side by side | Pass — headings, table, task list, code fence, quote, links all render |
| 3 | Live preview updates | Typed `" added by test"` into the editor | Pass — preview text updated within the debounce window, status changed to **Unsaved** |
| 4 | Save (Ctrl+S) | `^s` sent with editor focused | Pass — status returned to **Saved**; `added by test` present on disk exactly once |
| 5 | Unsaved-close prompt | Edited then clicked the caption Close button | Pass — Save / Don't save / Cancel dialog appeared; Cancel kept the window open |
| 6 | Find bar | Clicked **Find**, entered `preview` | Pass — find bar opens, field accepts input, document matches highlighted by the editor |
| 7 | Go to line | Opened dialog, entered `20`, confirmed | Pass — dialog validates (rejects out-of-range text) and caret lands on line 20 |
| 8 | Layout switching | Layout flyout → *Preview only* → *Split* | Pass — editor collapses, preview takes the full width, split restored |
| 9 | Theme switching | Theme button pressed | Pass after fix — whole window (toolbar, gutter, editor, preview) switches; light theme legible |
| 10 | Light-theme editor background | Screenshot inspection | Pass after fix — editor and gutter explicitly re-brush on theme change (see Known issues) |
| 11 | Large file | Opened a 1 048 512-character file | Pass — load + attach 397 ms, working set 293.4 MB, both panes rendered |
| 12 | Portable artifact | Extracted ZIP to a clean folder and launched | Pass — 336 ms activation, 23 ms file open, no `LumenMarkdown.exe.WebView2` folder beside the EXE |
| 13 | WebView2 data location | Inspected `%LOCALAPPDATA%\LumenMarkdown` | Pass — `WebView2`, `drafts`, `logs`, `settings.json` all under LocalAppData |
| 14 | Unit and integration tests | `dotnet test` (Release) | Pass — 20/20 |
| 15 | ZIP integrity | SHA-256 computed and recorded | Pass — `artifacts/LumenMarkdown-1.0.0-win-x64.zip.sha256` |

## Checks not performed

- **Clean machine / no .NET installed** — the .NET 10 Desktop Runtime prerequisite was
  not removed; framework-dependent startup on a bare machine is unverified.
- **External-change banner** — the file was not edited by a second process while open.
- **Draft recovery acceptance** — the prompt was dismissed (`Discard`) during automation;
  restore-and-continue was not walked through end to end.
- **Very large file (> 10 MB) refusal** — the 10 MB guard was not exercised.
- **Crash-path logging** — no forced unhandled exception was induced.
- **MSIX / signed distribution** — out of scope for v1 (portable ZIP only).

## Known issues found during the run

1. **Theme switch did not repaint the editor** (fixed). `RootGrid.RequestedTheme` alone did
   not re-resolve the RichEditBox template background; `Editor`, `GutterBorder` and the
   document grid now get explicit brushes in `ApplyTheme`.
2. **Published build crashed with `XamlParseException`** (fixed). `dotnet publish` omitted
   `LumenMarkdown.pri` and the `.xbf` files; a `CopyWinUIResourcesToPublish` target in the
   app project copies them after publish.
3. **WebView2 wrote its user-data folder beside the EXE** (fixed). The service now sets
   `WEBVIEW2_USER_DATA_FOLDER` to `%LOCALAPPDATA%\LumenMarkdown\WebView2` before
   initialising the control.
4. **Automation limitation, not a product bug**: `SendKeys` does not reach ContentDialog
   text boxes; the go-to-line check used UI Automation `ValuePattern` instead.
