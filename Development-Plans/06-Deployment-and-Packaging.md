# 06 — Deployment and Packaging

[← Testing](05-Testing.md) · [Contents](00-Contents.md) · [Research and Package Assessment →](07-Research-and-Package-Assessment.md)

---

## 1. Deployment profile (agreed)

| Question | Answer | Source |
|----------|--------|--------|
| Application type | WinUI 3 desktop app, unpackaged | UD-3 |
| Target OS | Windows 11, x64 | UD-2 |
| Packaging | Portable ZIP, no installer | UD-3 |
| Signing | Unsigned for v1 | UD-3 |
| .NET runtime | Framework-dependent — requires .NET 10 Desktop Runtime | UD-3 + D-4 |
| Windows App SDK | **Self-contained inside the app folder** | D-4 (assumed; proven in Phase 0) |
| Channel | Manual hand-out / internal download | A-5 |
| Auto-update | None in v1 | A-5 |

**D-16 — Split the two "framework-dependent" meanings.** "Framework-dependent" normally refers to .NET. Windows App SDK self-contained avoids a second runtime prerequisite (the Windows App Runtime installer), which is the most common cause of "app won't start" for unpackaged WinUI 3. If Phase 0 shows self-contained is unreliable, fall back to shipping the Windows App Runtime installer alongside the ZIP and document it.

## 2. Prerequisites on the target machine

| Prerequisite | Version | Check | If missing |
|--------------|---------|-------|------------|
| Windows 11 | 22000+ (x64) | `winver` | Unsupported |
| .NET 10 Desktop Runtime (x64) | 10.0.14 or later 10.0.x | `dotnet --list-runtimes` shows `Microsoft.WindowsDesktop.App 10.0.x` | App shows a host error; README links to the download |
| Microsoft Edge WebView2 Runtime | Evergreen, any current | Registry `HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}` or `Get-AppxPackage *WebView2*` | Preview pane banner; editing still works |
| Visual C++ runtime | Bundled with WinAppSDK/app local | — | Included in publish output |

No admin rights are required to run the app or write settings.

## 3. Build and publish

### 3.1 Project properties (Release)

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net10.0-windows10.0.22000.0</TargetFramework>
  <TargetPlatformMinVersion>10.0.22000.0</TargetPlatformMinVersion>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <Platforms>x64</Platforms>
  <WindowsPackageType>None</WindowsPackageType>
  <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
  <SelfContained>false</SelfContained>
  <PublishSingleFile>false</PublishSingleFile>
  <PublishReadyToRun>false</PublishReadyToRun>
  <EnableMsixTooling>false</EnableMsixTooling>
  <ApplicationManifest>app.manifest</ApplicationManifest>
  <Version>1.0.0</Version>
</PropertyGroup>
```

`PublishSingleFile` is **off** deliberately: WinUI 3 + WebView2 native assets are unreliable in a single file, and startup extraction hurts NFR-01.

### 3.2 Publish sequence (run later)

```powershell
dotnet restore LumenMarkdown.sln
dotnet build LumenMarkdown.sln -c Release -p:Platform=x64
dotnet test tests/LumenMarkdown.Core.Tests -c Release --no-build
dotnet publish src/LumenMarkdown.App -c Release -r win-x64 --self-contained false `
  -p:Platform=x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true `
  -p:PublishReadyToRun=false -o artifacts/publish/win-x64
```

Verification after publish:

```powershell
Get-ChildItem artifacts/publish/win-x64 | Measure-Object -Property Length -Sum
Test-Path artifacts/publish/win-x64/LumenMarkdown.exe
Test-Path artifacts/publish/win-x64/Microsoft.WindowsAppRuntime.Bootstrap.dll
```

The exact file list is confirmed at Phase 7, not assumed here. A missing bootstrap DLL or `Microsoft.ui.xaml.dll` means the self-contained property did not apply.

## 4. ZIP layout

```
LumenMarkdown-1.0.0-win-x64/
├── LumenMarkdown.exe
├── LumenMarkdown.dll
├── LumenMarkdown.Core.dll
├── *.dll                      (WinAppSDK, WebView2 loader, dependencies)
├── Assets/                    (preview.css, preview.js, icon)
├── README.txt                 (how to run, prerequisites, known issues)
├── LICENSE
├── THIRD-PARTY-NOTICES.md
└── CHANGELOG.md
```

Settings, logs and drafts are **never** stored beside the exe; they live in `%LOCALAPPDATA%\LumenMarkdown` so the folder can be read-only and upgrades never touch user data.

## 5. Versioning

- SemVer: `MAJOR.MINOR.PATCH`; v1 release = `1.0.0`.
- Assembly/file version from `<Version>`; window title shows `1.0.0` in About only, not in the title bar.
- Settings `schemaVersion` bumps only on breaking shape changes, with a migration branch and test.
- Git tag `v1.0.0` at the commit that produced the ZIP; SHA-256 recorded in release notes.

## 6. Signing intent and SmartScreen

v1 is **unsigned** by user decision. Consequences to state plainly in `README.txt`:

- Windows SmartScreen may show "Windows protected your PC" on first run; the user must choose **More info → Run anyway**.
- Some enterprise environments block unsigned executables outright; this is expected and out of scope.
- SHA-256 published so recipients can verify the download.

Future path (not v1): Authenticode sign `LumenMarkdown.exe` with an EV/OV certificate, timestamp via RFC 3161, then package as MSIX for Store or App Installer distribution. The code does not change; only the packaging and identity layer.

## 7. Upgrade and rollback

**Upgrade (portable):** close app → replace folder contents → launch. Settings schema is forward-compatible within 1.x; a newer schema on an older build triggers backup + defaults rather than corruption.

**Rollback:** keep the previous ZIP (`LumenMarkdown-0.9.x-win-x64.zip`) in the release archive. Restore by replacing the folder. User settings are untouched by rollback; if the newer build migrated `settings.json`, restore `settings.bad.json`/backup manually — documented in release notes.

No registry keys, services, or scheduled tasks are created, so uninstall is "delete the folder" plus optional removal of `%LOCALAPPDATA%\LumenMarkdown`.

## 8. Release checklist (Phase 7)

1. Clean `dotnet build -c Release`; zero errors, no new warnings.
2. Unit/integration tests green.
3. Publish folder inspected: exe, Core dll, WinAppSDK natives, WebView2 loader present.
4. Launch on clean Windows 11 x64 VM with only .NET 10 Desktop Runtime installed.
5. Full acceptance checklist ([10](10-Acceptance-Checklist.md)) executed and signed.
6. ZIP created, SHA-256 computed, release notes written.
7. `README.txt` states prerequisites and the SmartScreen warning.
8. Previous ZIP archived for rollback.
9. Tag and archive source.

## 9. Troubleshooting matrix

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| Dialog: "This application requires the .NET Desktop Runtime" | .NET 10 Desktop Runtime missing | Install x64 Desktop Runtime 10.0.x |
| App exits immediately, Event Viewer shows `0xc0000135` | WinAppSDK natives not in folder | Re-publish with `WindowsAppSDKSelfContained=true`; verify DLLs |
| Window opens, preview blank | WebView2 runtime missing | Install Evergreen WebView2 Runtime; banner should have warned |
| Preview blank but runtime present | CSP or `base href` broke the shell | Check `logs\`; disable CSP temporarily in Debug only |
| Settings not saved | `%LOCALAPPDATA%` write blocked (policy) | Log shows access denied; app runs with in-memory settings |
| SmartScreen blocks launch | Unsigned binary | More info → Run anyway; publish hash |
| Antivirus quarantine | Unsigned, low-reputation binary | Submit for false-positive review; consider signing earlier |

## 10. CI (optional, later)

A minimal GitHub Actions job on `windows-latest` can run restore/build/test and upload the ZIP artifact. It cannot validate the UI; the clean-VM gate stays manual. Do not add CI in v1 unless the user asks — it is not a requirement, and it adds a signing/secrets surface the project does not yet need.
