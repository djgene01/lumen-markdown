# Implementation status

All eight phases in `Development-Plans/phase-plan.json` have been implemented and the
Release build, test suite and portable ZIP have been produced and exercised.

| Phase | Scope | State | Evidence |
|-------|-------|-------|----------|
| P0 | Solution, projects, CI-ready build | Done | `LumenMarkdown.slnx`, `global.json`, Debug and Release builds succeed |
| P1 | Shell window, command bar, status bar, theming | Done | Screenshots; 336–559 ms activation |
| P2 | File open/save, encoding, dirty tracking, dialogs | Done | Save round-trip; unsaved-close prompt; core tests |
| P3 | Markdig → WebView2 preview pipeline | Done | `showcase.md` rendered; live update while typing |
| P4 | Editor: gutter, highlighting, find, go-to-line | Done | Find bar and go-to-line verified via UI Automation |
| P5 | Settings, recent files, session restore, drafts | Done | `%LOCALAPPDATA%\LumenMarkdown\settings.json`; draft folder |
| P6 | Layout modes, themes, zoom, external-change banner | Done | Layout and theme switching verified; theme repaint fixed |
| P7 | Tests, performance, packaging | Done | 20/20 tests; `docs/performance.md`; 54.7 MB ZIP + SHA-256 |

## Verification shortcuts

- `docs/acceptance-run.md` — the checks actually performed, with the gaps listed.
- `docs/performance.md` — measured start-up, open, memory and package sizes.
- `scripts/publish.ps1` — reproduces build → test → publish → trim → ZIP → hash.

## Deliberate deferrals

Carried over from the plans, not defects: tabs/multi-document, MSIX and code signing,
rendering raw HTML (escaped by design), a plugin surface, and a `TextControlBox` editor
swap (the `IEditorAdapter` seam exists for it).
