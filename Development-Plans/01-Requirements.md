# 01 — Requirements

[← Contents](00-Contents.md) · [Architecture →](02-Architecture.md)

**Product:** Lumen Markdown (working title)
**Platform:** WinUI 3 desktop app, .NET 10, Windows 11 x64
**Requirement baseline:** 1.0 — approved user decisions UD-1…UD-5

---

## 1. Purpose

Lumen Markdown is a fast local Markdown reader and editor for Windows 11. It opens `.md` files from disk, renders them faithfully, lets the user edit the source with syntax highlighting, and shows a live preview side by side. It is not a note-sync service, not a wiki, and not a collaborative editor.

The product exists to make one job pleasant: **read and edit a Markdown file on this PC without a browser or a cloud account.**

## 2. Users and primary scenarios

| Persona | Scenario | Success looks like |
|---------|----------|--------------------|
| Developer | Opens a README from a cloned repo, scans headings and code blocks | File opens in < 1 s, code blocks readable, no encoding damage |
| Writer | Drafts a document in Markdown with live preview | Typing latency invisible, preview follows within ~300 ms, Ctrl+S saves |
| Reviewer | Reads a long spec, jumps by heading | Scroll position and typography make long documents comfortable |
| Occasional user | Double-clicks a `.md` file (future file association) | App launches and shows the document |

## 3. Scope

### 3.1 In scope for v1

- Open a single Markdown file via file picker, drag-and-drop, and command line.
- Render CommonMark plus GitHub-flavoured extensions (tables, task lists, strikethrough, autolinks, footnotes).
- Edit the Markdown source with a monospaced editor, line numbers and Markdown syntax highlighting.
- Split view with synchronised scrolling and a live preview that updates while typing.
- Save, Save As, Save All (single document), with dirty-state protection on close.
- Recent files, window geometry, theme preference and preview layout persisted as JSON.
- Light/dark/system theme, adjustable editor and preview font size, zoom.
- Find in document (Ctrl+F) and go-to-line (Ctrl+G).

### 3.2 Out of scope for v1 (explicit non-requirements)

| Not building | Why |
|--------------|-----|
| Multiple tabs / multi-document workspace | Single-document v1 keeps state and testing small; revisit after v1 |
| Cloud sync, accounts, telemetry upload | No user requirement; local-only privacy stance |
| Plugin system or custom Markdown extensions | Not requested; Markdig pipeline is fixed |
| WYSIWYG / rich-text editing | The editor edits Markdown source, not rendered rich text |
| PDF/HTML export, printing | Deferred; not in the user's stated scope |
| Mermaid/LaTeX rendering inside preview | Deferred; would add a second rendering dependency |
| Windows 10 support | User chose Windows 11 only |
| ARM64 build | User chose x64 only |
| MSIX / Store signing | User chose unsigned portable ZIP for v1 |
| Auto-update | Manual ZIP replacement in v1 |

## 4. Functional requirements

Priority: **M** must, **S** should, **C** could (v1.1).

| ID | Pri | Requirement | Acceptance signal |
|----|-----|-------------|-------------------|
| FR-01 | M | Open a `.md`/`.markdown`/`.txt` file through the Windows file picker | Picker filters Markdown; chosen file content appears in the editor |
| FR-02 | M | Open a file dropped onto the window | Drop overlay appears; dropped file loads |
| FR-03 | M | Open a file passed as the first command-line argument | Launching `LumenMarkdown.exe notes.md` loads that file |
| FR-04 | M | Render Markdown to formatted HTML in the preview pane | Headings, emphasis, lists, links, tables, task lists, code blocks and quotes render correctly |
| FR-05 | M | Edit Markdown source in a monospaced editor | Text is editable; caret, selection, undo/redo behave normally |
| FR-06 | M | Update the preview automatically while typing | Preview reflects edits within 300 ms of the last keystroke |
| FR-07 | M | Highlight Markdown syntax in the editor | Headings, emphasis, links, code spans/fences and lists are visually distinguished |
| FR-08 | M | Save the current document; Save As for new documents | Ctrl+S writes to disk; status bar confirms; reopening shows identical bytes for unchanged text |
| FR-09 | M | Track dirty state and prompt before discarding | Title shows `•`; closing with unsaved edits asks Save / Discard / Cancel |
| FR-10 | M | Display line numbers and current line/column | Status bar updates on caret move |
| FR-11 | M | Toggle split / editor-only / preview-only layout | Toggle changes panes; state persists |
| FR-12 | M | Find text in the editor with next/previous and match count | Ctrl+F opens find bar; matches highlighted; Esc closes |
| FR-13 | M | Go to line | Ctrl+G accepts a line number and moves the caret |
| FR-14 | S | Recent files list (max 10) on the start surface and File menu | Reopening a recent file works after restart; missing files are removed |
| FR-15 | S | Persist window size/position, maximised state, pane splitter ratio | Restart restores geometry |
| FR-16 | S | Remember last theme and font size | Restart keeps selection |
| FR-17 | S | Synchronised scrolling between editor and preview | Scrolling the editor moves the preview proportionally and vice versa |
| FR-18 | S | Open http/https links from the preview in the default browser | Clicking a link opens the browser; the app itself does not navigate away |
| FR-19 | S | Reload from disk when the file changed externally, with confirmation | Prompt offers Reload / Keep my version |
| FR-20 | C | Export rendered HTML | Deferred to v1.1 |
| FR-21 | C | Print preview | Deferred to v1.1 |

### 4.1 Markdown dialect

- **CommonMark 0.31.2** as the base.
- Markdig advanced extensions: tables, task lists, footnotes, autolinks, strikethrough, emphasis extras, definition lists, YAML front-matter (rendered as a metadata block, not raw text).
- Raw HTML in Markdown: **disabled in v1** — Markdig renders embedded HTML as escaped text, never as live markup. Reason: a `.md` file is untrusted input and a sanitiser is a new attack surface. An allow-list sanitiser is a v1.1 candidate, not a v1 requirement.
- Relative image and link paths resolve against the document's folder; missing images show alt text plus a placeholder.

### 4.2 File handling requirements

| Aspect | Requirement |
|--------|-------------|
| Encoding | Detect UTF-8 (with/without BOM), UTF-16 LE/BE; default UTF-8 when unknown; never silently replace invalid bytes |
| Line endings | Preserve the file's dominant line ending (CRLF/LF) on save |
| Size | Open files up to 10 MB without freezing the UI; above 10 MB warn and open read-only |
| Binary/undecodable | Detect NUL bytes; refuse with a clear message instead of showing garbage |
| Paths | Long paths (>260 chars) supported via `\\?\`-safe APIs; UNC paths allowed |
| Permissions | Read-only files open in read-only mode with a visible badge; Save As remains available |

## 5. Non-functional requirements

| ID | Requirement | Target | How verified |
|----|-------------|--------|--------------|
| NFR-01 | Cold start to usable window | ≤ 2.5 s on a mid-range SSD laptop | Stopwatch from process start to first paint; 5 runs, median |
| NFR-02 | Open 1 MB Markdown file | ≤ 1.0 s to rendered preview | Timed instrumentation, 5 runs |
| NFR-03 | Keystroke-to-preview latency | ≤ 300 ms p95 for a 100 KB document | Debounce instrumentation over 200 edits |
| NFR-04 | Typing responsiveness | No input lag > 50 ms in a 1 MB document | Manual + frame timing during scroll/typing |
| NFR-05 | Memory | ≤ 350 MB working set with a 1 MB document open | Task Manager / `Process.WorkingSet64` |
| NFR-06 | Stability | No unhandled exception across the 30-minute soak script | Soak run, crash log empty |
| NFR-07 | Accessibility | Keyboard-only operation of every command; Narrator reads labels; 4.5:1 text contrast | Accessibility Insights + keyboard walkthrough |
| NFR-08 | DPI | Correct rendering at 100 %, 150 %, 200 % and mixed-DPI multi-monitor | Visual check on scaled displays |
| NFR-09 | Privacy | Zero network calls in normal operation except user-initiated link opens | Fiddler/Resource Monitor check during a session |
| NFR-10 | Localisation-ready | All user-visible strings in resources; en-ZA/en-US shipped | Resource audit; no hard-coded strings in XAML code-behind |
| NFR-11 | Recoverability | Crash or kill loses at most the last autosave interval (60 s) | Kill process, relaunch, recover draft |
| NFR-12 | Footprint | ZIP ≤ 90 MB; installed folder ≤ 250 MB | Measure publish output |

## 6. User decisions (authoritative)

| ID | Decision |
|----|----------|
| UD-1 | Balanced reader + editor: split live preview, syntax highlighting, open/save |
| UD-2 | Windows 11 only, x64 |
| UD-3 | Portable unpackaged ZIP, framework-dependent, unsigned |
| UD-4 | Greenfield; no existing code or assets; placeholder branding |
| UD-5 | No database; JSON settings + recent files |

## 7. Assumptions and design decisions that shape requirements

| ID | Statement | Impact if wrong |
|----|-----------|-----------------|
| A-1 | Single document per window is sufficient for v1 | Tabs become a significant architecture change (document host abstraction already planned, so cost is moderate) |
| A-2 | Preview is HTML in WebView2, not a native XAML renderer | If WebView2 runtime cannot be assumed, preview must be redesigned or runtime bootstrapped |
| A-3 | Source syntax highlighting is required; code-fence highlighting **inside preview** is optional v1.1 | Adds ColorCode/highlight.js work |
| A-4 | The app may write settings under `%LOCALAPPDATA%\LumenMarkdown` | Sandboxed/MSIX later changes paths |
| A-5 | No telemetry, no auto-update in v1 | Release process stays manual |
| A-6 | Placeholder name "Lumen Markdown" and icon until branding is supplied | Renaming touches package metadata, window title, settings folder |
| D-1 | Markdig for parsing; HTML fragment rendered by WebView2 | — |
| D-2 | JSON settings via `System.Text.Json` with source generation | — |
| D-3 | `net10.0-windows10.0.22000.0`, `TargetPlatformMinVersion 10.0.22000.0` | — |
| D-4 | Windows App SDK **self-contained**, .NET **framework-dependent** (see [06](06-Deployment-and-Packaging.md)) | Tester still needs .NET 10 Desktop Runtime; alternative is a larger self-contained ZIP |

## 8. Constraints

- Windows App SDK 2.6.x stable is the only supported UI stack for this plan; preview/experimental channels are excluded.
- Unpackaged WinUI 3 requires either the Windows App Runtime installed or self-contained deployment; the plan chooses self-contained WinAppSDK (D-4) and must prove it in Phase 0.
- The editor control must be usable with keyboard and Narrator; a third-party editor control is acceptable only if it passes the Phase 4 accessibility check.
- No network dependency at runtime. Fonts and CSS ship with the app.
- Documentation is produced under Plan Mode; no application code is written in this phase.

## 9. Acceptance criteria (v1)

1. All **M** requirements pass the checks in [10-Acceptance-Checklist.md](10-Acceptance-Checklist.md).
2. All **S** requirements pass or have a signed, documented deferral.
3. NFR-01…NFR-05 measured within target on the reference machine.
4. The ZIP produced by [06-Deployment-and-Packaging.md](06-Deployment-and-Packaging.md) runs on a clean Windows 11 x64 VM with only the .NET 10 Desktop Runtime installed.
5. No P1/P2 defect open; no unhandled exception in the soak test.
6. User validation gate (Phase 7) signed off.

## 10. Traceability

| Requirement group | Design | Implementation phase | Test |
|-------------------|--------|----------------------|------|
| FR-01…FR-03 open | [03 §4](03-UX-and-Editor-Design.md) | 2 | T-OPEN-* |
| FR-04, FR-06 render/preview | [02 §5](02-Architecture.md) | 2, 3 | T-RENDER-* |
| FR-05, FR-07 edit/highlight | [03 §5](03-UX-and-Editor-Design.md) | 3, 4 | T-EDIT-* |
| FR-08…FR-10 save/dirty | [02 §6](02-Architecture.md) | 5 | T-SAVE-* |
| FR-11…FR-19 UX | [03 §3–7](03-UX-and-Editor-Design.md) | 4, 5 | T-UX-* |
| NFR-01…NFR-12 | [05](05-Testing.md), [06](06-Deployment-and-Packaging.md) | 6, 7 | T-PERF-*, T-SOAK |
