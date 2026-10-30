# 03 — UX and Editor Design

[← Architecture](02-Architecture.md) · [Contents](00-Contents.md) · [Phased Implementation →](04-Phased-Implementation.md)

---

## 1. Design intent

The window should read like a focused desktop writing tool: one document, generous typography, no chrome competing with the text. Reader mode is the same window with the editor collapsed. The app never opens a modal for routine work except the native file picker and the unsaved-changes prompt.

## 2. Window anatomy

```
┌────────────────────────────────────────────────────────────────────┐
│  Lumen Markdown — spec.md •                    ─   □   ✕          │  custom title bar (drag region)
├────────────────────────────────────────────────────────────────────┤
│  Open  Save  Save As │ Undo  Redo │ Find  Layout ▾ │ ☀/☾          │  command bar (48 px)
├───────────────────────────────┬────────────────────────────────────┤
│  1  # Project Spec            │  Project Spec                      │
│  2                            │  ─────────────                     │
│  3  Intro paragraph with      │  Intro paragraph with **bold**     │
│  4  **bold** and `code`.      │  and code.                         │
│                               │                                    │
│         editor pane           │        preview pane (WebView2)     │
├───────────────────────────────┴────────────────────────────────────┤
│ ● Unsaved   Ln 4, Col 21   312 words   UTF-8 · CRLF   100%        │  status bar (28 px)
└────────────────────────────────────────────────────────────────────┘
```

- Minimum window 900 × 600; default 1280 × 820.
- Splitter ratio persists; double-click resets to 50/50.
- At < 1000 px width the default layout automatically becomes editor-only with a "Preview" toggle, so text never becomes a 200 px column.

## 3. Commands

| Command | Shortcut | Enabled when | Behaviour |
|---------|----------|--------------|-----------|
| Open… | Ctrl+O | always | Native picker, filter `.md;.markdown;.txt`; prompts if current document is dirty |
| Save | Ctrl+S | document open and writable | Writes in place; for untitled routes to Save As |
| Save As… | Ctrl+Shift+S | document open | Picker defaults to current name/folder |
| Close document | Ctrl+W | document open | Dirty prompt, then start surface |
| Undo / Redo | Ctrl+Z / Ctrl+Y | editor focus | Editor control's stack |
| Find | Ctrl+F | document open | Find bar; Enter next, Shift+Enter previous, Esc close |
| Go to line | Ctrl+G | document open | Small inline input; validates range |
| Toggle layout | Ctrl+Shift+P | document open | Split → editor → preview → split |
| Zoom in/out/reset | Ctrl+ + / - / 0 | always | Preview zoom 50–200 %, editor font 10–28 pt |
| Toggle theme | Ctrl+Shift+L | always | Light ↔ Dark (System remains a settings option) |
| Reload from disk | F5 | file-backed | Confirmation; discards unsaved edits only after consent |
| About / shortcuts | F1 | always | Non-modal flyout listing shortcuts |

Command bar shows icon + label for Open/Save/Save As; icon-only with tooltips for Undo/Redo/Find/Layout. All commands have `KeyboardAccelerator`s declared in XAML, not in code-behind.

## 4. Opening and the start surface

When no document is open the document area shows a calm start surface:

- Drop zone text: "Drop a Markdown file here".
- Primary button **Open file**.
- Recent files (max 10) with filename, folder, and last-opened time; missing entries render disabled with "Not found" and are pruned on click attempt.
- Shortcut hints (Ctrl+O, F1).

Drag-and-drop is handled at window level (`AllowDrop`, `DragOver` sets `Copy`, `Drop` validates extension and single-file only). A non-Markdown file shows a toast: "Lumen Markdown opens .md, .markdown and .txt files."

Command-line activation reads `Environment.GetCommandLineArgs()[1]`; if it is a valid path, load after the window activates. Unknown/invalid path shows a non-blocking info bar.

## 5. Editor design

### 5.1 Requirements the control must meet

1. Monospaced text, 1.5 line height, soft wrap toggle.
2. Line numbers gutter.
3. Markdown token colouring: heading, bold/italic markers, link text/URL, inline code, fenced code, list markers, block quote, horizontal rule.
4. Large-document behaviour: 1 MB file remains scrollable and typable; virtualised rendering preferred.
5. Standard editing: caret, selection, undo/redo, clipboard, IME, right-to-left text, Ctrl+Backspace word delete.
6. Accessibility: exposed as a text edit control to Narrator/UIA; keyboard reachable; high-contrast theme respected.
7. No licence cost; source available preferred.

### 5.2 Options assessed

| Option | Fit | Risks | Verdict |
|--------|-----|-------|---------|
| **`RichEditBox`** (built-in) | Text editing + character formatting via `ITextDocument`; no line numbers; highlighting requires re-applying formats per change | Formatting churn on large docs; selection/undo interference | **Fallback**, guaranteed to work, Phase 1 baseline |
| **`TextBox`** | Simplest, fast, accessible; no per-token colour, no gutter | No syntax highlighting | Phase 1 only |
| **TextControlBox-WinUI** (MIT, community) | Purpose-built WinUI 3 editor: syntax highlighting, line numbers, search, word wrap | Small project (26 stars, single maintainer); API churn (1.7.0, Oct 2026); accessibility unproven | **Primary candidate**, gated by Phase 0 spike + Phase 4 accessibility check |
| **WebView2 + Monaco/CodeMirror** | Best highlighting and editing maturity | Whole editor becomes web content: IME, UIA, clipboard, theming and keyboard integration all need bridging; heavier memory | Rejected for v1; revisit if native editor blocks delivery |

**D-15 — Build against an `IEditorAdapter` interface.** Phase 1 wraps `TextBox`; Phase 4 swaps in `TextControlBox` behind the same interface (`Text`, `SelectionStart`, `SelectionLength`, `ScrollRatio`, `SetHighlighting`, `FindNext`). If the spike fails, the app still ships on `RichEditBox` with headings/emphasis-only highlighting, and that limitation is recorded in the release notes.

### 5.3 Highlighting strategy

- Tokenise on the UI thread only for the **visible viewport plus 200 lines** (the control's own highlighter, or a lightweight Markdig-based line classifier for `RichEditBox`).
- Re-highlight debounced at 80 ms after edit; full-document re-highlight only on open and on theme change.
- Colours come from theme resources (`EditorHeadingBrush`, `EditorCodeBrush`, …), so light/dark/high-contrast switch without restarting.
- Never block typing on highlighting; if the classifier falls behind, drop intermediate passes.

### 5.4 Find bar

Non-modal bar docked above the editor: query box, match counter ("3 of 17"), previous/next, match-case toggle, close. Search runs over the in-memory text with `StringComparison.Ordinal` (case-insensitive by default). Zero matches shows "No results" in the counter. Esc returns focus to the editor at the original caret.

## 6. Preview design

### 6.1 Typography

- Body 16 px / 1.65, max measure 72 ch, left-aligned (not justified).
- Heading scale 2.0 / 1.55 / 1.3 / 1.15 / 1.0 rem with 0.6 em top margin.
- Code: `Cascadia Mono` (present on Windows 11), fallback `Consolas`; inline code on a subtle tinted background; fenced blocks with a copy button (optional, Phase 4).
- Tables: full width, zebra rows, horizontal scroll inside the block rather than page-wide overflow.
- Block quotes: 3 px accent left border, muted text.
- Task lists: disabled checkboxes (rendering only; editing task state is **not** a v1 feature).
- Images: max-width 100 %, height auto, alt text shown when missing.

### 6.2 Preview shell

```html
<!doctype html>
<html data-theme="dark">
<head>
  <meta charset="utf-8">
  <meta http-equiv="Content-Security-Policy"
        content="default-src 'none'; img-src file: data:; style-src 'unsafe-inline'; font-src file:">
  <base href="file:///C:/path/to/document/folder/">
  <link rel="stylesheet" href="file:///.../Assets/preview.css">
</head>
<body><article id="content"><!-- fragment --></article></body>
</html>
```

- The shell is rebuilt only when the document path or theme changes.
- Updates replace `#content.innerHTML` via `postMessage` and restore `scrollTop` ratio.
- `base href` makes relative images and links resolve against the document, not the app folder.

### 6.3 Synchronised scrolling

Map editor scroll ratio `r = scrollTop / (scrollHeight - clientHeight)` to preview `r'`. Because block heights differ, use heading anchors when available: build an anchor map (source line → element `data-source-line`) during rendering, then interpolate between the nearest anchors. Fall back to plain ratio when no anchors exist. This is a **should** requirement (FR-17); plain ratio is acceptable for v1 if anchor mapping slips.

### 6.4 Theme

`RequestedTheme` on the root element plus `data-theme` on the preview `<html>`. System theme changes are observed via `ActualThemeChanged` and pushed to the preview without reloading the document.

## 7. Status bar and feedback

| Segment | Content | Updates |
|---------|---------|---------|
| Dirty | `● Unsaved` / `Saved` / `Read-only` | edit, save, open |
| Caret | `Ln 12, Col 4` / `Ln 12, Col 4 (5 selected)` | selection change |
| Words | word count (whitespace-delimited, code fences included) | debounced 300 ms |
| Encoding | `UTF-8 · CRLF` | open/save |
| Zoom | `100%` | zoom command |

Long operations (> 400 ms) show an indeterminate `ProgressRing` in the status bar, never a modal. Errors use `InfoBar` at the top of the document area with a single action ("Retry", "Open folder", "Details").

## 8. Empty, error and edge states

| State | UI |
|-------|----|
| No document | Start surface with drop zone, Open, recents |
| File missing on open | InfoBar: "File not found" + path; stay on start surface |
| Access denied | InfoBar: "You don't have permission to open this file" + "Open containing folder" |
| Binary file | Dialog: "This doesn't look like a text file" |
| > 10 MB | Dialog: open read-only? Yes/No |
| Read-only file | Status badge; Save disabled; Save As enabled; title unchanged |
| Encoding invalid bytes | Open with replacement characters, banner "Some characters could not be decoded. Saving will change the file." |
| WebView2 missing | Banner in preview: "Microsoft Edge WebView2 Runtime is required" + link to Microsoft's download page |
| Render failure | Keep last good preview; banner "Preview could not update" + Details |

## 9. Accessibility

- Every command reachable by keyboard; focus order: title bar → command bar → editor → preview → status.
- `AutomationProperties.Name` on all icon buttons.
- Preview content is web content: set `CoreWebView2` accessibility to enabled; headings render as real `h1…h6`.
- Contrast: body text ≥ 4.5:1, large headings ≥ 3:1 in both themes; verify with Accessibility Insights.
- `TextScaleFactor` up to 200 % must not clip the command bar; labels collapse to icons at 150 %.
- Respect "Show animations in Windows" (reduce transitions).

## 10. Visual assets and branding (placeholder)

| Asset | v1 placeholder | Notes |
|-------|----------------|-------|
| App name | Lumen Markdown | Working title (A-6) |
| Icon | 256 px rounded-square with "M" glyph, generated as SVG→PNG | Replaced when branding arrives |
| Accent | System accent colour | No custom brand palette yet |
| Preview CSS | Bundled, theme-aware | Ships in `Assets/preview.css` |
| Fonts | Segoe UI Variable (UI), Cascadia Mono (code) | Both present on Windows 11; no download |

## 11. UX acceptance checks (summary)

1. Window opens at 1280 × 820 with split layout and empty start surface.
2. Ctrl+O → picker → file renders in both panes; title shows filename.
3. Typing updates preview within 300 ms without losing scroll position.
4. Ctrl+S saves; dirty dot clears; reopening shows identical content.
5. Closing with unsaved edits offers Save / Discard / Cancel, and Cancel returns to the document.
6. Layout toggle cycles three states and survives restart.
7. 200 % text scale and 200 % DPI show no clipped commands or overlapping text.
8. Narrator reads the command bar buttons and the editor as an editable text field.

Full checklist: [10-Acceptance-Checklist.md](10-Acceptance-Checklist.md).
