# Performance and size measurements

Measured on the development machine (Windows 11 build 26300, x64) with the Release
build, 30 October 2026. Figures are single-run observations, not a benchmark suite.

| Metric | Target | Measured | Verdict |
|--------|--------|----------|---------|
| Cold start to window activated | ≤ 2 500 ms | 336–559 ms (four runs) | Pass |
| Open 949-char Markdown (load + attach) | n/a | 23–44 ms | Pass |
| Open 1 048 512-char (~1 MB) Markdown (load + attach) | ≤ 1 000 ms | 397 ms | Pass |
| Working set with 1 MB document open | ≤ 350 MB | 293.4 MB | Pass |
| ZIP size | ≤ 90 MB | 54.7 MB | Pass |
| Published folder | ≤ 250 MB | 155 MB | Pass |
| Unit/integration tests | all green | 20 passed | Pass |

## Notes

- The 1 MB measurement covers file load plus editor attachment. The preview render
  follows asynchronously; it was visible in the verification screenshot within the
  same second.
- Syntax highlighting is intentionally skipped above 4 000 token spans, so a 1 MB
  document opens fast but loses token colouring. This is a documented v1 trade-off.
- Working set was read with `Process.WorkingSet64` after the document had rendered.
- The published folder includes the self-contained Windows App SDK, WebView2 loader,
  and the Windows App SDK AI runtime components that ship with the package. ZIP
  compression brings 155 MB down to 54.7 MB.

## Commands used

```powershell
dotnet build src/LumenMarkdown.App/LumenMarkdown.App.csproj -c Release -p:Platform=x64
dotnet publish src/LumenMarkdown.App/LumenMarkdown.App.csproj -c Release -r win-x64 `
  --self-contained false -p:Platform=x64 -p:WindowsPackageType=None `
  -p:WindowsAppSDKSelfContained=true -p:PublishReadyToRun=false -o artifacts/publish/win-x64
```
