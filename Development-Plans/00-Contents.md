# Lumen Markdown — Development and Implementation Manuals

**Product:** Lumen Markdown — a WinUI 3 Markdown editor and reader for Windows
**Stack:** .NET 10 · WinUI 3 (Windows App SDK 2.6.x) · C# · XAML · WebView2 preview
**Target:** Windows 11, x64 · unpackaged, framework-dependent ZIP · unsigned v1
**Status:** Planning baseline, revision 1.0 (30 October 2026)

> **Scope note.** These manuals are the complete planning set. They contain no application source code and no executed build results. Every "command" and "check" below is an instruction to run later, during implementation.

> **Implementation record (30 October 2026).** All eight phases have since been implemented and verified; see [../docs/IMPLEMENTATION-STATUS.md](../docs/IMPLEMENTATION-STATUS.md), [../docs/acceptance-run.md](../docs/acceptance-run.md) and [../docs/performance.md](../docs/performance.md). The manuals remain the baseline specification.

---

## Table of contents

| # | Manual | What it answers |
|---|--------|-----------------|
| 0 | **00-Contents.md** (this file) | Reading order, decision record, diagram index, conventions |
| 1 | [01-Requirements.md](01-Requirements.md) | Functional/non-functional requirements, user decisions, assumptions, out of scope, acceptance criteria |
| 2 | [02-Architecture.md](02-Architecture.md) | Layers, projects, UI-thread ownership, preview pipeline, framework layout diagram |
| 3 | [03-UX-and-Editor-Design.md](03-UX-and-Editor-Design.md) | Window layout, commands and shortcuts, editor/preview behaviour, theming, accessibility |
| 4 | [04-Phased-Implementation.md](04-Phased-Implementation.md) | Eight phases, each a runnable slice with files, commands, UI checks and exit gates |
| 5 | [05-Testing.md](05-Testing.md) | Test strategy, unit/integration/UI matrix, performance and failure diagnostics |
| 6 | [06-Deployment-and-Packaging.md](06-Deployment-and-Packaging.md) | Build/publish, ZIP layout, runtime prerequisites, signing intent, upgrade/rollback |
| 7 | [07-Research-and-Package-Assessment.md](07-Research-and-Package-Assessment.md) | Package and open-source survey with versions, licences and cited sources |
| 8 | [08-Project-Size-Assessment.md](08-Project-Size-Assessment.md) | LOC, effort, complexity and cost envelope |
| 9 | [09-Notes-Risks-and-Open-Questions.md](09-Notes-Risks-and-Open-Questions.md) | Constraints, risks, mitigations, open prerequisites |
| 10 | [10-Acceptance-Checklist.md](10-Acceptance-Checklist.md) | Requirement-by-requirement sign-off checklist |
| — | [phase-plan.json](phase-plan.json) | Machine-readable phases with verification steps and user validation gates |

---

## Decisions supplied by the user

| ID | Decision | Value |
|----|----------|-------|
| UD-1 | Feature scope and priority | Balanced: reader + editor with split live preview, syntax highlighting, open/save |
| UD-2 | Target platform | Windows 11 only, x64 |
| UD-3 | Distribution and signing | Portable unpackaged ZIP, framework-dependent, unsigned for v1 |
| UD-4 | Existing code/assets | None — greenfield; placeholder app icon and branding |
| UD-5 | Persistence | No database; JSON settings and recent-file list via `System.Text.Json` |

Everything not in that table is a **documented assumption or design decision** (prefix `A-` or `D-`), not a user requirement. The full register is in [09-Notes-Risks-and-Open-Questions.md](09-Notes-Risks-and-Open-Questions.md).

## Baseline stack (verified live on 30 Oct 2026)

| Item | Version | Evidence |
|------|---------|----------|
| .NET | 10.0.14 runtime, SDK 10.0.403 current; 10.0.400/401 installed on this host | .NET releases index; `dotnet --list-sdks` |
| .NET 11 | 11.0.0-rc.3 (go-live, 25 Oct 2026) — **not used** | .NET releases index |
| Windows App SDK / WinUI 3 | Stable 2.6.4 (27 Oct 2026) | Microsoft Learn release channels |
| Markdig | 1.5.1 | NuGet flat-container index |
| WebView2 SDK | 1.0.4314.35 | NuGet flat-container index |
| Target framework | `net10.0-windows10.0.22000.0` | Design decision D-3 |

Details and alternatives: [07-Research-and-Package-Assessment.md](07-Research-and-Package-Assessment.md).

---

## Diagrams

All diagrams are Mermaid sources rendered to PDF (for print/review) and PNG (for quick viewing). Editable `.mmd` sits beside each pair.

| Diagram | Artifacts | Referenced from |
|---------|-----------|-----------------|
| Application framework and window layout | [PDF](Diagrams/application-framework-layout-affc92150eb44713901300492adbbc47/diagram.pdf) · [PNG](Diagrams/application-framework-layout-affc92150eb44713901300492adbbc47/diagram.png) · [MMD](Diagrams/application-framework-layout-affc92150eb44713901300492adbbc47/diagram.mmd) | [02-Architecture](02-Architecture.md), [03-UX](03-UX-and-Editor-Design.md) |
| Document lifecycle (open → edit → preview → save) | [PDF](Diagrams/document-lifecycle-32936d6450e34c03b7412e29b4626075/diagram.pdf) · [PNG](Diagrams/document-lifecycle-32936d6450e34c03b7412e29b4626075/diagram.png) · [MMD](Diagrams/document-lifecycle-32936d6450e34c03b7412e29b4626075/diagram.mmd) | [02-Architecture](02-Architecture.md), [05-Testing](05-Testing.md) |
| Phase dependency map | [PDF](Diagrams/phase-delivery-map-c0d45dd54f764456bd4ac64b5f816469/diagram.pdf) · [PNG](Diagrams/phase-delivery-map-c0d45dd54f764456bd4ac64b5f816469/diagram.png) · [MMD](Diagrams/phase-delivery-map-c0d45dd54f764456bd4ac64b5f816469/diagram.mmd) | [04-Phased-Implementation](04-Phased-Implementation.md) |

---

## Recommended reading order

1. **Requirements** — what v1 must do, and what it explicitly must not do.
2. **Architecture** — the three layers, the projects, and who owns the UI thread.
3. **UX and editor design** — the exact window, commands and preview behaviour to build.
4. **Phased implementation** — the build order; each phase is a demoable slice.
5. **Testing** — how each slice is proven before moving on.
6. **Deployment** — how the ZIP is produced and what the tester's machine needs.
7. **Research and package assessment** — why these libraries, with sources.
8. **Project size assessment** — effort and risk envelope.
9. **Notes, risks, open questions** — read before committing to a date.
10. **Acceptance checklist** — the definition of done.

## Documentation conventions

- `FR-xx` functional requirement, `NFR-xx` non-functional requirement, `UD-x` user decision, `A-x` assumption, `D-x` design decision, `R-x` risk.
- "Exit criteria" is binary and observable. "UI check" means a human-visible behaviour, not a compile success.
- Paths such as `src/LumenMarkdown.App/` describe the **intended application layout**; they are not evidence that files exist.
- No phase may be marked complete on build success alone; the observable check in that phase must pass.

## Document control

| Revision | Date | Change |
|----------|------|--------|
| 1.0 | 2026-10-30 | Initial planning baseline, greenfield WinUI 3 / .NET 10 |
