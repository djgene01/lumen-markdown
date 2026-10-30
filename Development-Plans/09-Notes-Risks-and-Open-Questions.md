# 09 — Notes, Risks and Open Questions

[← Project Size Assessment](08-Project-Size-Assessment.md) · [Contents](00-Contents.md) · [Acceptance Checklist →](10-Acceptance-Checklist.md)

---

## 1. Implementation notes

1. **Do Phase 0 before promising a date.** The toolchain gap (SDK 10.0.403 vs installed 10.0.401) and unpackaged self-contained WinAppSDK are the two unknowns that can invalidate the schedule.
2. **Treat Markdown as untrusted input.** Raw HTML is disabled, CSP is strict, and links are scheme-checked before `Launcher.LaunchUriAsync`.
3. **Never block the UI thread.** File I/O and Markdig run on the pool; only XAML and WebView2 touch the UI thread.
4. **Hash, don't flag.** Dirty state is `sha256(current) != sha256(saved)`; it removes a whole class of state bugs and is cheap for ≤ 10 MB.
5. **Debounce with coalescing, not queues.** One in-flight render plus one pending flag; generations discard stale results.
6. **Settings are a contract.** Version the schema, back up corrupt files, and never crash on parse failure.
7. **The preview shell is content, not code.** Keep `preview.css` and the template in `Assets/` so design changes do not touch C#.
8. **Line endings are user data.** Detect, display, and preserve; do not "normalise" silently.
9. **Log without content.** Log file names/operations/errors, never document text or paths beyond the file name where avoidable.
10. **One observable check per phase.** A green build is not a phase exit.

## 2. Documented assumptions (not requirements)

| ID | Assumption | Confirm by |
|----|------------|------------|
| A-1 | One document per window is enough for v1 | Phase 1 gate |
| A-2 | WebView2 Evergreen is available on target machines | Phase 0 / 2 |
| A-3 | Preview code-fence colouring is optional | Phase 4 |
| A-4 | Settings may live in `%LOCALAPPDATA%\LumenMarkdown` | Phase 1 |
| A-5 | No telemetry, no auto-update | Release gate |
| A-6 | "Lumen Markdown" and the placeholder icon are temporary | Branding decision |

## 3. Risks

| ID | Risk | Likelihood | Impact | Mitigation | Owner |
|----|------|-----------|--------|------------|-------|
| R-01 | Unpackaged self-contained WinAppSDK fails to launch on a clean machine | Medium | High | Phase 0 spike; fallback = ship Windows App Runtime installer + README | Dev |
| R-02 | WebView2 runtime missing or broken on target | Low | Medium | Detect at startup, banner with download link; editor and save still work | Dev |
| R-03 | TextControlBox is unstable, inaccessible, or slow at 1 MB | Medium-High | High | `IEditorAdapter`; fallback `RichEditBox` with reduced highlighting; Phase 0/4 gates | Dev + user |
| R-04 | Live preview latency or scroll jumping | Medium | High | Debounce + coalescing, anchor mapping, measured p95 gate | Dev |
| R-05 | Encoding or line-ending corruption on save | Low | **Critical** | Encoding matrix tests, atomic writes, byte-comparison check at gate 5 | Dev |
| R-06 | SDK 10.0.403 unavailable / pin mismatch | Medium | Low | Install SDK or relax `global.json` in Phase 0 | Dev |
| R-07 | Scope creep (tabs, export, Mermaid) | High | Medium | Explicit out-of-scope list in [01 §3.2](01-Requirements.md); changes go to v1.1 | User + dev |
| R-08 | Unsigned binary blocked by SmartScreen/AV | Medium | Medium | Document the warning; publish SHA-256; consider signing if distribution grows | User |
| R-09 | Large-file cliffs (10 MB, 50k lines) | Medium | Medium | Read-only guard, size limits, stress corpus in Phase 6 | Dev |
| R-10 | Placeholder branding ships by accident | Low | Low | Release checklist item; rename touches metadata and settings folder (migration note) | User |
| R-11 | Third-party licence or package ID mismatch | Low | Medium | Verify licence/package before Phase 4; vendor source if necessary | Dev |
| R-12 | Markdig update changes HTML output | Low | Low | Structural golden tests; pin version centrally | Dev |

## 4. Open questions (blocking or near-blocking)

| ID | Question | Needed by | Default if unanswered |
|----|----------|-----------|-----------------------|
| Q-1 | Will the product keep the name "Lumen Markdown"? | Phase 7 | Keep working title; placeholder icon |
| Q-2 | Is a 45–80 MB ZIP acceptable for distribution? | Phase 7 | Yes (framework-dependent .NET, self-contained WinAppSDK) |
| Q-3 | Should v1.1 add tabs, export, or preview code colouring first? | After gate 7 | Tabs, because it changes document hosting |
| Q-4 | Is signing budget available if SmartScreen becomes a problem? | Before public distribution | Ship unsigned with documented warning |
| Q-5 | Which machine is the reference for NFR measurements? | Phase 6 | The development laptop, spec recorded in `docs/performance.md` |
| Q-6 | Does the user want a Store/MSIX path later? | Post-v1 | Not planned; architecture does not block it |

## 5. Open prerequisites (must be closed before coding)

1. `dotnet --list-sdks` shows 10.0.4xx — install 10.0.403 **or** change `global.json` to roll forward to 10.0.401. Decide and record.
2. Confirm the WinUI 3 project template path: Visual Studio "Blank App, Unpackaged (WinUI 3 in Desktop)" or a CLI template discovered via `dotnet new list winui`. The plan does not assume a CLI template exists.
3. Confirm WinUI 3 / Windows App SDK 2.6.4 packages restore on this machine (`dotnet restore` of the blank template).
4. Confirm the TextControlBox package identity or plan to reference its source.
5. Confirm a clean Windows 11 x64 VM (or spare machine) is available for Phase 0 and Phase 7.
6. Confirm the .NET 10 Desktop Runtime installer is obtainable for the clean-VM test.

## 6. Explicit non-goals repeated here

No cloud, no accounts, no sync, no plugin API, no WYSIWYG, no Windows 10, no ARM64, no MSIX/Store in v1, no auto-update, no telemetry.

## 7. Notes about how this documentation was produced

- This is **Plan Mode output**: manuals and diagrams only. No application code was written, no packages installed, no build or test executed.
- The host did not expose a `planning_document` tool in this session. The manuals were therefore written as plain `.md` files under `Development-Plans` with the file tools, preserving the required names and structure.
- The host did not expose `render_mermaid_chart`. Diagrams were authored as `.mmd` and rendered locally with Mermaid 11 in headless Chrome to PDF and PNG, keeping the `Diagrams/<slug>-<hash>/diagram.*` layout. They are real rendered artifacts, not mock-ups; the folder names were generated for this plan rather than returned by that tool.
- Consequently, no claim is made that the app builds, runs, or passes any check. Every command in these manuals is an instruction for a later implementation session.
