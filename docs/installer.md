# Installer (MSI)

The WiX Toolset v7 installer wraps the same published payload as the portable ZIP,
adding a Start Menu shortcut and Add/Remove Programs registration.

- **Artifact:** `artifacts/LumenMarkdown-1.0.0-win-x64.msi` (44 MB)
- **SHA-256:** `bf2faea45c717b574ec9bcceb86c9370aa24cbdedccdb3e0286396bb871316b4`
- **Scope:** per-machine — `%ProgramFiles%\Lumen Markdown`, shortcut for all users
- **Elevation:** required (UAC prompt on install and uninstall)
- **Upgrade:** same `UpgradeCode` across versions; a newer MSI replaces the older one
- **Uninstall:** Settings → Apps → Installed apps → Lumen Markdown, or
  `msiexec /x artifacts\LumenMarkdown-1.0.0-win-x64.msi`

## Build

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish.ps1        # payload
powershell -ExecutionPolicy Bypass -File scripts/build-installer.ps1
```

`gen-installer.ps1` harvests the publish folder into
`installer/generated/AppFiles.wxs`: 185 files across 11 subdirectories, one component
per file with a path-derived stable ID and an auto-generated component GUID. The WiX
source uses `Scope="perMachine"` and the `ProgramFiles64Folder` standard directory.

`build-installer.ps1` adds the `WixToolset.UI.wixext` extension globally and builds
from inside `installer/`, because WiX resolves `Assets\app.ico` and `License.rtf`
relative to the working directory. It writes a SHA-256 file beside the MSI.

## Verification performed

- **Build:** `wix build` exited 0. The MSI Property table contains `ALLUSERS=1`,
  `ProductName=Lumen Markdown` and `ProductVersion=1.0.0`.
- **Administrative install:** `msiexec /a … TARGETDIR=artifacts\admin-image` exited 0
  and laid down 186 files, including
  `PFiles64\Lumen Markdown\LumenMarkdown.exe`. The file table and cabinet are valid.
- **Real install/uninstall:** **not executed.** This session runs without administrator
  rights (`IsInRole(Administrator)` = False); a per-machine install fails with MS error
  1925 (insufficient privileges). Run
  `msiexec /i artifacts\LumenMarkdown-1.0.0-win-x64.msi` from an elevated prompt to
  confirm the UAC path, the Start Menu shortcut and uninstall.

## Prerequisites

The MSI does not chain the runtimes. A machine without them installs successfully but
the app will not start:

- .NET 10 Desktop Runtime (x64)
- Microsoft Edge WebView2 Runtime (present on Windows 11 by default)

A future revision could add a `LaunchCondition` with a registry search or bundle the
runtime, but v1 keeps the MSI a thin wrapper over the tested payload.

## Notes and limitations

- Settings, logs, recents, drafts and the WebView2 profile live in
  `%LOCALAPPDATA%\LumenMarkdown` and survive uninstall by design.
- The MSI is unsigned; SmartScreen may warn on download. Sign with `signtool` before
  distributing.
- No `.md` file association is registered; the app is opt-in via Open With.
- A per-user variant (installs to `%LOCALAPPDATA%\Programs`, no UAC) was built and
  installed during this session, but WiX 7 marks the package UAC-compliant, so Windows
  Installer ignored `MSIINSTALLPERUSER` and wrote a machine-wide registration anyway.
  Per-machine is therefore the honest configuration for this package.
