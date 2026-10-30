Lumen Markdown 1.0.0 — portable build
=====================================

Requirements
------------
- Windows 11, 64-bit
- .NET 10 Desktop Runtime (x64): https://dotnet.microsoft.com/download/dotnet/10.0
- Microsoft Edge WebView2 Runtime (normally already installed on Windows 11)

Run
---
Double-click LumenMarkdown.exe, or pass a file:
  LumenMarkdown.exe "C:\docs\README.md"

The app stores settings, logs and crash-recovery drafts in:
  %LOCALAPPDATA%\LumenMarkdown

No installer, no registry keys, no admin rights. To remove, delete this folder and
optionally %LOCALAPPDATA%\LumenMarkdown.

Unsigned build
--------------
This build is not code-signed. Windows SmartScreen may show
"Windows protected your PC" on first launch; choose "More info" then "Run anyway".
Verify the download against the SHA-256 published with the release.

Known limitations
-----------------
- One document per window.
- Raw HTML in Markdown is shown as text, not rendered.
- Syntax highlighting pauses for very large documents (> ~4,000 tokens).
- Preview requires the WebView2 runtime; editing and saving work without it.

Third-party components
----------------------
See THIRD-PARTY-NOTICES.md.
