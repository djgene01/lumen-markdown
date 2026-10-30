# 08 — Project Size Assessment

[← Research and Package Assessment](07-Research-and-Package-Assessment.md) · [Contents](00-Contents.md) · [Notes, Risks and Open Questions →](09-Notes-Risks-and-Open-Questions.md)

All numbers are **estimates for planning**, not measurements. The workspace contains no code, so nothing here is derived from an actual build.

---

## 1. Size summary

| Dimension | Estimate |
|-----------|----------|
| Projects | 3 (app, core library, test project) |
| Hand-written C# (app + core) | ~3,300 LOC |
| XAML (views, styles, resources) | ~900 lines |
| Tests (C#) | ~900 LOC |
| CSS/JS for preview shell | ~250 lines |
| Total hand-written | **~4,700–5,300 LOC** |
| Third-party runtime dependencies | 2 primary (Markdig, WebView2 SDK) + WinAppSDK |
| Build artefacts | 1 exe + ~40–80 DLLs/native files, ZIP ~45–80 MB |
| Complexity | **Medium** (single-window desktop app with one hard subsystem: live preview) |

## 2. Component breakdown

| Component | Est. LOC | Difficulty | Notes |
|-----------|---------:|------------|-------|
| `Core/Documents` (session, state machine, dirty hash) | 300 | Medium | Concurrency and state transitions; high test value |
| `Core/Markdown` (pipeline, template, limits) | 250 | Medium | Markdig config is small; security and limits are the work |
| `Core/IO` (encoding, atomic write, paths) | 250 | Medium-High | Encoding edge cases and Windows path rules |
| `Core/Settings` (JSON, recents, migration) | 150 | Low | Source-generated serialisation |
| `Core/Logging` | 80 | Low | Rolling file, no content |
| Shell: `MainWindow`, title bar, command bar, status bar | 450 | Medium | Custom title bar and DPI behave differently than expected |
| `EditorPane` + adapters + highlighting | 500 | **High** | Third-party control integration, viewport tokenising |
| `PreviewPane` + WebView2 bridge + scroll sync | 350 | **High** | Lifetime, messaging, CSP, anchors |
| ViewModels and commands | 400 | Medium | Re-entrancy and enablement |
| Services (pickers, theme, window state, watcher, drafts) | 450 | Medium | `InitializeWithWindow`, `FileSystemWatcher` quirks |
| Find / go-to-line / status widgets | 250 | Medium | Selection and focus handling |
| XAML styles, brushes, theme resources | 300 | Low-Medium | Two themes + high contrast |
| Tests | 900 | Medium | Fixtures and golden HTML |
| Packaging scripts/docs | 100 | Low | ZIP + notes |
| **Total** | **~4,730** | | |

## 3. Effort estimate

| Phase | Effort (person-days) | Uncertainty |
|-------|---------------------:|-------------|
| 0 Prerequisites and spikes | 3–5 | High (runtime/toolchain unknowns) |
| 1 Shell and window | 4–6 | Medium |
| 2 Open and read | 5–7 | Medium |
| 3 Edit and live preview | 5–7 | High (WebView2 messaging, scroll) |
| 4 Highlighting and editing UX | 6–9 | **High** (editor control risk) |
| 5 Save, recents, recovery | 5–7 | Medium |
| 6 Hardening and performance | 6–8 | High (depends on measured cliffs) |
| 7 Packaging and release | 3–4 | Medium |
| **Total** | **37–53 days** | |

**Calendar guidance:** 8–11 weeks full-time for one developer, or 12–16 weeks at ~60 % allocation. Add 15–20 % contingency for the editor control and self-contained WinAppSDK unknowns.

**Cost:** rate-dependent; at a planning rate of 1 unit/day the envelope is 37–53 units, plus ~2 days of user validation time across gates. No licence costs: all selected libraries are MIT or in-box.

## 4. What drives the estimate

| Driver | Effect |
|--------|--------|
| Live preview correctness (scroll, caret, stale renders) | The single largest schedule risk; Phases 3 and 6 |
| Editor control maturity | If TextControlBox fails, fallback `RichEditBox` highlighting adds 3–5 days and reduces fidelity |
| Encoding/line-ending fidelity | Small code, large test matrix; skipping it causes data-loss bugs |
| Unpackaged WinUI 3 deployment | One bad day in Phase 0 can save two in Phase 7; do not skip the spike |
| Single document scope | Keeps the app at ~5k LOC; tabs would add ~1,500 LOC and a state redesign |

## 5. What would change the size materially

| Change | Delta |
|--------|-------|
| Add tabs / multi-document | +1,200–1,800 LOC, +2–3 weeks |
| MSIX packaging + signing + auto-update | +1 week, plus certificate and Store process |
| WYSIWYG editing | Rewrite of the editor layer; not a delta, a different product |
| Mermaid/LaTeX in preview | +3–5 days, +bundle size, +security surface |
| Windows 10 + ARM64 support | +3–5 days build/CI/test matrix |
| Export to HTML/PDF | +3–4 days |

## 6. Confidence

- **High confidence** in the 4,700–5,300 LOC range (± 15 %) because scope is narrow and the architecture is conventional.
- **Medium confidence** in the 8–11 week full-time estimate; the editor control and WebView2 behaviours are the variance.
- **Low confidence** in any date before Phase 0 completes; the toolchain and self-contained deployment are unproven on this machine.
