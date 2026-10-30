# 10 — Acceptance Checklist

[← Notes, Risks and Open Questions](09-Notes-Risks-and-Open-Questions.md) · [Contents](00-Contents.md) · [phase-plan.json](phase-plan.json)

Use on the **clean Windows 11 x64 VM** with only the .NET 10 Desktop Runtime installed. Tick each row only with observed evidence; a code review is not evidence. Record the build hash and date at the top.

| Field | Value |
|-------|-------|
| Build / ZIP | `LumenMarkdown-1.0.0-win-x64.zip` |
| SHA-256 | ____________________ |
| Machine / Windows build | ____________________ |
| Display scale | ____________________ |
| Tester / date | ____________________ |

---

## A. Functional requirements

| ID | Check | Pass criteria | Result |
|----|-------|---------------|--------|
| FR-01 | Ctrl+O, pick `showcase.md` | Picker filters Markdown; content appears in editor and preview | ☐ |
| FR-02 | Drag a `.md` file onto the window | Drop overlay; document loads | ☐ |
| FR-03 | Launch `LumenMarkdown.exe "C:\samples\README.md"` | Document loads after window activates | ☐ |
| FR-04 | Inspect fixture with headings, tables, task list, code, quote, footnote, strikethrough | All render correctly; no raw Markdown syntax visible in preview | ☐ |
| FR-05 | Type, select, undo, redo, paste | Standard editing; no lost characters; undo restores | ☐ |
| FR-06 | Type a sentence and pause | Preview updates within 300 ms; caret stays put | ☐ |
| FR-07 | View headings, bold, links, code, lists in editor | Tokens visually distinguished in light and dark themes | ☐ |
| FR-08 | Ctrl+S then close and reopen | File contains edits; status shows Saved; bytes match expectation | ☐ |
| FR-09 | Edit then close window | Prompt Save/Discard/Cancel; Cancel keeps document and caret | ☐ |
| FR-10 | Move caret across lines; wrap a long line | Line/column updates; gutter numbers align | ☐ |
| FR-11 | Ctrl+Shift+P three times | Split → editor → preview → split; state persists after restart | ☐ |
| FR-12 | Ctrl+F, search a known word | Match count correct; next/previous wrap; Esc restores caret | ☐ |
| FR-13 | Ctrl+G, enter valid/invalid values | Valid jumps; `0`, `abc`, out-of-range rejected with message | ☐ |
| FR-14 | Open three files, restart, use recents | Recents persist in MRU order, max 10, missing pruned | ☐ |
| FR-15 | Resize/move/maximise, restart | Geometry and maximised state restored | ☐ |
| FR-16 | Change theme and font size, restart | Both restored | ☐ |
| FR-17 | Scroll editor, then preview | Other pane follows proportionally (or documented limitation) | ☐ |
| FR-18 | Click an http link in preview | Default browser opens; app does not navigate away | ☐ |
| FR-19 | Modify open file in Notepad | Stale banner appears; Reload loads new content after confirmation | ☐ |

## B. Non-functional requirements

| ID | Check | Pass criteria | Result |
|----|-------|---------------|--------|
| NFR-01 | Cold start ×5 | Median ≤ 2.5 s to usable window | ☐ |
| NFR-02 | Open 1 MB file ×5 | Median ≤ 1.0 s to rendered preview | ☐ |
| NFR-03 | 200 keystrokes with instrumentation | p95 ≤ 300 ms | ☐ |
| NFR-04 | Type/scroll 1 MB document | No perceived lag > 50 ms | ☐ |
| NFR-05 | Working set after 5 min | ≤ 350 MB | ☐ |
| NFR-06 | 30-minute soak loop | No unhandled exception; stable memory | ☐ |
| NFR-07 | Keyboard-only + Narrator + Accessibility Insights | All commands reachable; names announced; no contrast failures | ☐ |
| NFR-08 | 100/150/200 % and mixed-DPI monitors | No clipping, blur, or misplaced chrome | ☐ |
| NFR-09 | Monitor network during a session | No connections except user-opened links | ☐ |
| NFR-10 | Resource audit | No hard-coded user-visible strings in code-behind | ☐ |
| NFR-11 | Kill process after editing, relaunch | Draft offered and restored (≤ 60 s loss) | ☐ |
| NFR-12 | Measure ZIP and folder | ZIP ≤ 90 MB; folder ≤ 250 MB | ☐ |

## C. Edge and error states

| Check | Pass criteria | Result |
|-------|---------------|--------|
| Open missing file path | InfoBar "File not found"; no crash | ☐ |
| Open file without read permission | Clear access-denied message | ☐ |
| Open `.exe` | "Doesn't look like a text file"; no garbage | ☐ |
| Open 10 MB+ file | Read-only warning and usable UI | ☐ |
| Read-only `.md` | Badge shown; Save disabled; Save As works | ☐ |
| Invalid UTF-8 bytes | Replacement chars + warning banner | ☐ |
| Save to read-only folder | Clean failure; edits retained | ☐ |
| Delete file while open, then Save | Save As offered; no crash | ☐ |
| Corrupt `settings.json` | Defaults + `settings.bad.json` | ☐ |
| Missing WebView2 runtime | Banner with guidance; editing/saving still work | ☐ |
| External file change while dirty | Stale state preserved; no silent overwrite | ☐ |

## D. Deployment

| Check | Pass criteria | Result |
|-------|---------------|--------|
| Extract ZIP to path with spaces | Launches | ☐ |
| First run without admin | Settings/logs created under `%LOCALAPPDATA%` | ☐ |
| Clean VM with only .NET 10 Desktop Runtime | Full app works | ☐ |
| Delete app folder | No required registry/service residue | ☐ |
| Re-run after deleting settings | Defaults; no crash | ☐ |
| SHA-256 matches published value | Match | ☐ |
| SmartScreen behaviour documented in README | Warning text present | ☐ |

## E. Requirement coverage summary

| Group | Total | Passed | Deferred (approved) | Failed |
|-------|------:|-------:|--------------------:|-------:|
| Must (FR-01…FR-13) | 13 | | 0 allowed | |
| Should (FR-14…FR-19) | 6 | | | |
| NFR-01…NFR-12 | 12 | | | |
| Edge states | 11 | | | |
| Deployment | 7 | | | |

## F. Defect gate

| Severity | Open | Accepted? |
|----------|-----:|-----------|
| P1 (crash/data loss) | 0 required | — |
| P2 (major feature broken) | 0 required | — |
| P3 (minor/cosmetic) | | ☐ reviewed |

## G. Sign-off

| Role | Name | Decision | Date |
|------|------|----------|------|
| Implementer | | Ready for release | |
| User / product owner | | ☐ Accept ☐ Reject (reasons attached) | |

**Release is authorised only when every Must row is ticked, every Should row is ticked or explicitly deferred in writing, and the user has signed section G.**
