param(
    [string]$Version = "1.0.0",
    [string]$PublishDir = "artifacts/publish/win-x64"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not (Test-Path (Join-Path $PublishDir "LumenMarkdown.exe"))) {
    throw "Publish output not found at $PublishDir. Run scripts/publish.ps1 first."
}

powershell -NoProfile -ExecutionPolicy Bypass -File scripts/gen-installer.ps1 -PublishDir $PublishDir
wix extension add -g WixToolset.UI.wixext | Out-Null

$msi = Join-Path $root "artifacts/LumenMarkdown-$Version-win-x64.msi"
Push-Location (Join-Path $root "installer")
try {
    wix build LumenMarkdown.wxs generated/AppFiles.wxs `
        -ext WixToolset.UI.wixext -arch x64 -o $msi
}
finally {
    Pop-Location
}

$hash = (Get-FileHash $msi -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path -Leaf $msi)" | Set-Content "$msi.sha256"
Write-Host "MSI: $msi"
Write-Host "SHA256: $hash"
