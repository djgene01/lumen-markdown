# 07 — Research and Package Assessment

[← Deployment and Packaging](06-Deployment-and-Packaging.md) · [Contents](00-Contents.md) · [Project Size Assessment →](08-Project-Size-Assessment.md)

Research window: **30 October 2026**. Versions were read from the NuGet flat-container API and Microsoft Learn on that date; they will drift. Re-check before pinning.

---

## 1. Platform and runtime findings

| Finding | Detail | Source |
|---------|--------|--------|
| .NET 10 is current and supported | 10.0.14 (SDK 10.0.403), support phase **active**; latest patches dated 25 Oct 2026 | [.NET releases index](https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json) |
| .NET 11 is not GA | 11.0.0-rc.3, go-live, 25 Oct 2026 — available but preview; excluded by D-3 | same |
| Host SDKs | 10.0.400 and 10.0.401 installed; the plan pins 10.0.403 (or relaxes the pin) | `dotnet --list-sdks` on this machine |
| Windows App SDK stable | **2.6.4**, released 27 Oct 2026; WinUI 3 is available on the Stable channel | [Release channels](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-channels) |
| Preview/experimental | 2.0 Preview2 and 2.5 Experimental exist; neither is used | same |
| WinUI 3 unpackaged deployment | Supported; requires the Windows App Runtime or self-contained deployment | [Deploy unpackaged apps](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps) |
| Deployment overview | Packaged vs unpackaged trade-offs (identity, updates, file associations) | [Deployment overview](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview) |
| WebView2 distribution | Evergreen Runtime is present on modern Windows; Fixed Version can be bundled at a size cost | [WebView2 distribution](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution) |

**Consequence for the plan:** target `net10.0-windows10.0.22000.0`, Windows App SDK 2.6.x stable, WebView2 Evergreen. Do not adopt Windows App SDK 2.0 Preview2 or .NET 11 RC — both would break the "supported baseline" assumption for no feature benefit.

## 2. Markdown parsing and rendering

### 2.1 Markdig — **selected**

- NuGet **1.5.1**; targets `net8.0`, `netstandard2.0`, `net462` — runs on .NET 10.
- CommonMark-compliant, extensible pipeline, advanced extensions (tables, task lists, footnotes, autolinks, strikethrough, YAML front-matter), fast parser.
- MIT licence; actively maintained (0.45 → 1.5.1 across the last year).
- Sources: [GitHub](https://github.com/xoofx/markdig) · [NuGet](https://www.nuget.org/packages/Markdig) · [docs](https://xoofx.github.io/markdig/)

**Use:** `MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build()`, then `Markdown.ToHtml`. Disabling raw HTML is the v1 security posture ([02 §11](02-Architecture.md)).

### 2.2 CommunityToolkit Labs `MarkdownTextBlock` — **not selected**

- An experiment to rebuild `MarkdownTextBlock` on Markdig with `RichTextBlock` as the rendering target.
- Open questions in the discussion include RichTextBlock limitations, extensibility and accessibility; it lives in Labs, and no stable NuGet package is published under the expected IDs (flat-container lookups returned `BlobNotFound` on 30 Oct 2026).
- Source: [Labs-Windows discussion #536](https://github.com/CommunityToolkit/Labs-Windows/discussions/536)

**Verdict:** unsuitable as a v1 dependency (experimental, no supported package). It confirms the native-XAML rendering path is still immature, which supports D-1 (HTML in WebView2).

### 2.3 READU.md — **reference, not dependency**

- WinUI 3 Markdown reader/editor, Fluent Design, WebView2, Mermaid support, MIT, ~20 stars, last release v2.3.1 (Oct 2026).
- Useful as a behavioural reference for window layout, file open/save and preview hosting; its code is not copied and no package is consumed.
- Source: [github.com/breezy89757/READU.md](https://github.com/breezy89757/READU.md)

### 2.4 AvalonEdit / MarkView.Avalonia — **rejected**

- AvalonEdit is WPF-oriented; MarkView.Avalonia targets Avalonia. Neither fits a WinUI 3 XAML tree without hosting workarounds. They are mentioned only to record that they were considered and rejected on platform fit.

## 3. Editor control

| Candidate | Evidence | Verdict |
|-----------|----------|---------|
| **TextControlBox-WinUI** | MIT; WinUI 3 text box with syntax highlighting, line numbering, search; v1.7.0 (24 Oct 2026); 358 commits, ~26 stars; single maintainer | **Primary, gated** by Phase 0 spike and Phase 4 accessibility/1 MB checks |
| `RichEditBox` | Built into WinUI 3; no gutter; highlighting needs per-change `ITextDocument` formatting | **Fallback**, guaranteed availability |
| `TextBox` | Built in, fast, accessible; no token colouring | Phase 1 placeholder only |
| WebView2 + Monaco/CodeMirror | Mature editing, but turns the editor into web content (IME, UIA, clipboard, theming bridges) | Rejected for v1 |

Source: [TextControlBox-WinUI](https://github.com/FrozenAssassine/TextControlBox-WinUI) (MIT licence stated in repo). Package ID on NuGet could not be confirmed from the flat-container API on 30 Oct 2026 — verify the exact package or vendor the source before Phase 4.

**Risk register entry:** R-03 (third-party editor control).

## 4. Preview host

| Item | Version / detail | Source |
|------|------------------|--------|
| WebView2 SDK | NuGet **1.0.4314.35** | [NuGet](https://www.nuget.org/packages/Microsoft.Web.WebView2) |
| Runtime | Evergreen; present on Windows 11 by default, but must be detected | [Distribution docs](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution) |
| Hosting model | `WebView2` XAML control in WinUI 3; `EnsureCoreWebView2Async`, `PostWebMessageAsString` | Microsoft Learn WebView2 guidance |

**Why not a native renderer:** see [02 §5.2](02-Architecture.md). The deciding factor is table/list/code fidelity with zero custom layout code.

## 5. Supporting libraries

| Need | Option | Version | Decision |
|------|--------|---------|----------|
| JSON settings | `System.Text.Json` (in-box) | .NET 10 | **Use**, source-generated context |
| MVVM helpers | CommunityToolkit.Mvvm | not pinned | **Not used** (D-5); hand-rolled `ObservableObject` avoids an unverified dependency |
| Code-fence highlighting in preview | ColorCode.Core | 2.0.15 | **Deferred** (v1.1); not required by UD-1 |
| Logging | `Microsoft.Extensions.Logging` + file sink, or a 60-line file logger | — | **Own minimal logger**; no Serilog dependency for one output file |
| Testing | xUnit + `Microsoft.NET.Test.Sdk` | latest stable at implementation | Use; pin in `Directory.Packages.props` |
| Installer | none (ZIP) | — | Per UD-3 |

Package IDs and versions above were confirmed via `api.nuget.org/v3-flatcontainer/<id>/index.json` except where marked unconfirmed.

## 6. Competitive / prior-art scan

| Project | Stack | Takeaway |
|---------|-------|----------|
| READU.md | WinUI 3 + WebView2 | Proves the WebView2 preview approach is practical for a WinUI 3 Markdown app |
| Markdig-based editors (Avalonia/WPF) | Markdig + native text box | Markdig is the common parsing choice; rendering is the hard part |
| MarkText / Typora (Electron) | Web tech | Feature benchmark for live preview and typography, not an architecture model |
| Notepad++ / VS Code | Native/web hybrids | Establish the expectation for find, go-to-line and large-file behaviour |

No open-source WinUI 3 project reviewed offers a stable, supported, native-XAML Markdown renderer; this is the central reason for the WebView2 decision.

## 7. Sources (all accessed 30 October 2026)

1. .NET release metadata — https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json
2. What's new in .NET 10 — https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview
3. Windows App SDK release channels — https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-channels
4. Deploy unpackaged apps — https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps
5. Deployment overview (packaged vs unpackaged) — https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview
6. WebView2 distribution — https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution
7. Markdig repository — https://github.com/xoofx/markdig
8. Markdig on NuGet — https://www.nuget.org/packages/Markdig
9. Markdig documentation — https://xoofx.github.io/markdig/
10. CommunityToolkit Labs MarkdownTextBlock discussion — https://github.com/CommunityToolkit/Labs-Windows/discussions/536
11. READU.md — https://github.com/breezy89757/READU.md
12. TextControlBox-WinUI — https://github.com/FrozenAssassine/TextControlBox-WinUI
13. Microsoft.Web.WebView2 on NuGet — https://www.nuget.org/packages/Microsoft.Web.WebView2
14. NuGet flat-container API (version checks) — https://api.nuget.org/v3-flatcontainer/

## 8. What the research does **not** establish

- That `WindowsAppSDKSelfContained=true` works cleanly for this app on a runtime-free machine (Phase 0 proves it).
- That TextControlBox is accessible or stable at 1 MB (Phase 0/4 prove it).
- That WebView2 is installed on every target machine (runtime detection is required regardless).
- Any performance number for this app; all targets are estimates until measured.
