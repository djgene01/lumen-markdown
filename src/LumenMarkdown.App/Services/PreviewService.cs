using System.Text.Json;
using LumenMarkdown.Core.Logging;
using LumenMarkdown.Core.Markdown;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace LumenMarkdown.App.Services;

/// <summary>Hosts the HTML preview in WebView2 and bridges messages.</summary>
public sealed class PreviewService
{
    private readonly WebView2 _webView;
    private readonly string _css;
    private readonly string _js;
    private readonly string _shellPath;
    private string? _currentDocument;
    private bool _initialized;

    public event Action<string>? OpenUrlRequested;
    public event Action<double>? Scrolled;

    public PreviewService(WebView2 webView)
    {
        _webView = webView;
        _css = ReadAsset("preview.css");
        _js = ReadAsset("preview.js");
        string dir = Path.Combine(Path.GetTempPath(), "LumenMarkdown");
        Directory.CreateDirectory(dir);
        _shellPath = Path.Combine(dir, $"preview-{Environment.ProcessId}.html");
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        string userData = Path.Combine(AppPaths.Root, "WebView2");
        Directory.CreateDirectory(userData);
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userData);
        await _webView.EnsureCoreWebView2Async();
        var core = _webView.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.NavigationStarting += (_, e) =>
        {
            if (!e.Uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
            }
        };
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                OpenUrlRequested?.Invoke(uri.AbsoluteUri);
            }
        };
        core.WebMessageReceived += (_, e) => HandleMessage(e.WebMessageAsJson);
        _initialized = true;
    }

    private void HandleMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeProp)) return;
            string type = typeProp.GetString() ?? string.Empty;
            if (type == "open" && root.TryGetProperty("url", out var urlProp))
            {
                string? url = urlProp.GetString();
                if (!string.IsNullOrWhiteSpace(url)) OpenUrlRequested?.Invoke(url);
            }
            else if (type == "scrolled" && root.TryGetProperty("ratio", out var ratioProp))
            {
                Scrolled?.Invoke(ratioProp.GetDouble());
            }
        }
        catch (JsonException ex)
        {
            AppLogger.Error("Invalid preview message", ex);
        }
    }

    public async Task RenderAsync(string fragment, string? documentPath, bool dark, bool forceReload = false)
    {
        if (!_initialized) await InitializeAsync();

        bool documentChanged = !string.Equals(_currentDocument, documentPath, StringComparison.OrdinalIgnoreCase);
        if (documentChanged || forceReload || !File.Exists(_shellPath))
        {
            string shell = MarkdownRenderer.BuildShell(fragment, documentPath, _css, _js, dark);
            await File.WriteAllTextAsync(_shellPath, shell);
            _currentDocument = documentPath;
            _webView.CoreWebView2.Navigate(new Uri(_shellPath).AbsoluteUri);
        }
        else
        {
            var payload = JsonSerializer.Serialize(new { type = "html", html = fragment });
            _webView.CoreWebView2.PostWebMessageAsJson(payload);
        }
    }

    public void SetTheme(bool dark)
    {
        if (!_initialized) return;
        _webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "theme", theme = dark ? "dark" : "light" }));
    }

    public void SetZoom(double zoom)
    {
        if (!_initialized) return;
        _webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "zoom", zoom }));
    }

    public void ScrollTo(double ratio)
    {
        if (!_initialized) return;
        _webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "scroll", ratio }));
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_shellPath)) File.Delete(_shellPath);
        }
        catch
        {
            // temp file cleanup is best effort
        }
    }

    private static string ReadAsset(string name)
    {
        string path = Path.Combine(AppPaths.AssetDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
