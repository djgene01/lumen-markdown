param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet build src/LumenMarkdown.App/LumenMarkdown.App.csproj -c $Configuration -p:Platform=x64
dotnet test tests/LumenMarkdown.Core.Tests -c $Configuration

$publish = "artifacts/publish/win-x64"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish src/LumenMarkdown.App/LumenMarkdown.App.csproj -c $Configuration -r win-x64 `
    --self-contained false -p:Platform=x64 -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true -p:PublishReadyToRun=false -o $publish

# Trim satellite resources and symbols to keep the portable ZIP small.
Get-ChildItem $publish -Directory |
    Where-Object { $_.Name -match '^[a-z]{2,3}(-[A-Za-z]{2,4}){1,2}$' -and $_.Name -ne 'en-us' } |
    Remove-Item -Recurse -Force
Get-ChildItem $publish -Filter *.pdb -Recurse | Remove-Item -Force

Copy-Item "Packaging/README.txt" $publish
Copy-Item "Packaging/THIRD-PARTY-NOTICES.md" $publish
Copy-Item "CHANGELOG.md" $publish
Copy-Item "LICENSE" $publish

$zip = "artifacts/LumenMarkdown-$Version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$publish/*" -DestinationPath $zip -CompressionLevel Optimal

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path -Leaf $zip)" | Set-Content "$zip.sha256"
Write-Host "ZIP: $zip"
Write-Host "SHA256: $hash"
