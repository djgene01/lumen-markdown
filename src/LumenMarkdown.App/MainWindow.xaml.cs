using System.Diagnostics;
using System.Text.Json;
using LumenMarkdown.App.Services;
using LumenMarkdown.Core.Documents;
using LumenMarkdown.Core.Editing;
using LumenMarkdown.Core.IO;
using LumenMarkdown.Core.Logging;
using LumenMarkdown.Core.Markdown;
using LumenMarkdown.Core.Settings;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Microsoft.UI.Text;
using WinRT.Interop;

using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;

namespace LumenMarkdown.App;

public sealed partial class MainWindow : Window
{
    private readonly SettingsStore _store = new(AppPaths.SettingsFile);
    private readonly DispatcherQueueTimer _renderTimer;
    private readonly DispatcherQueueTimer _highlightTimer;
    private readonly DispatcherQueueTimer _settingsTimer;
    private readonly DispatcherQueueTimer _draftTimer;
    private readonly DispatcherQueueTimer _watchTimer;
    private readonly DispatcherQueueTimer _wordTimer;

    private AppSettings _settings;
    private DocumentSession? _session;
    private PreviewService? _preview;
    private ScrollViewer? _editorScroll;
    private FileSystemWatcher? _watcher;

    private bool _suppressEditor;
    private bool _dark;
    private bool _previewReady;
    private bool _forceClose;
    private bool _loaded;
    private bool _syncingScroll;
    private int _renderGeneration;
    private int _matchIndex;
    private int _lastCaretLine = 1;
    private double _lineHeight = 20;
    private double _contentTop = 10;
    private double _splitRatio = 0.5;
    private string? _pendingInitialPath;
    private DateTime _lastWriteSeen = DateTime.MinValue;
    private string _editorTextCache = string.Empty;
    private List<(int Start, int Length)> _matches = new();

    public MainWindow()
    {
        InitializeComponent();
        Title = "Lumen Markdown";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);

        _settings = _store.Load();
        _splitRatio = Math.Clamp(_settings.SplitterRatio, 0.2, 0.8);

        _renderTimer = CreateTimer(TimeSpan.FromMilliseconds(250), RenderTick);
        _highlightTimer = CreateTimer(TimeSpan.FromMilliseconds(300), HighlightTick);
        _settingsTimer = CreateTimer(TimeSpan.FromMilliseconds(600), (_, _) => SaveSettingsNow());
        _draftTimer = CreateTimer(TimeSpan.FromSeconds(60), (_, _) => WriteDraft());
        _watchTimer = CreateTimer(TimeSpan.FromMilliseconds(600), (_, _) => CheckExternalChange());
        _wordTimer = CreateTimer(TimeSpan.FromMilliseconds(300), (_, _) => UpdateWordCount());

        ApplyTheme(initial: true);
        ApplyFontSize();
        ApplyLayout(_settings.Layout, persist: false);

        RootGrid.AllowDrop = true;
        RootGrid.DragOver += OnDragOver;
        RootGrid.Drop += OnDrop;

        AppWindow.Closing += OnWindowClosing;
        AppWindow.Changed += OnWindowChanged;

        Activated += OnFirstActivated;
        _draftTimer.Start();
        RefreshRecentList();
        UpdateCommandStates();
    }

    private DispatcherQueueTimer CreateTimer(TimeSpan interval, EventHandler<object> handler)
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = interval;
        timer.IsRepeating = false;
        timer.Tick += (s, e) => handler(s, e);
        return timer;
    }

    private async void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_loaded) return;
        _loaded = true;
        ApplyWindowGeometry();

        _preview = new PreviewService(Preview);
        _preview.OpenUrlRequested += async url => await Launcher.LaunchUriAsync(new Uri(url));
        _preview.Scrolled += ratio => SyncEditorScroll(ratio);
        try
        {
            await _preview.InitializeAsync();
            _previewReady = true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("WebView2 initialization failed", ex);
            ShowInfo(InfoBarSeverity.Warning, "Preview unavailable",
                "Microsoft Edge WebView2 Runtime is required for the preview. Editing and saving still work.");
        }

        if (!string.IsNullOrWhiteSpace(_pendingInitialPath))
        {
            string path = _pendingInitialPath!;
            _pendingInitialPath = null;
            await OpenFileAsync(path, checkDirty: false);
        }
        else
        {
            await OfferDraftRecoveryAsync();
        }
    }

    public void LoadInitialFile(string path)
    {
        if (_loaded) _ = OpenFileAsync(path, checkDirty: false);
        else _pendingInitialPath = path;
    }

    // ---------- Settings, theme, layout ----------

    private void ApplyTheme(bool initial = false)
    {
        bool dark = _settings.Theme switch
        {
            "Dark" => true,
            "Light" => false,
            _ => Application.Current.RequestedTheme == ApplicationTheme.Dark
        };
        _dark = dark;
        var elementTheme = _settings.Theme == "System"
            ? ElementTheme.Default
            : (dark ? ElementTheme.Dark : ElementTheme.Light);
        RootGrid.RequestedTheme = elementTheme;
        DocumentGrid.RequestedTheme = elementTheme;
        Editor.RequestedTheme = elementTheme;
        Editor.Background = new SolidColorBrush(dark
            ? Windows.UI.Color.FromArgb(255, 27, 27, 31)
            : Windows.UI.Color.FromArgb(255, 255, 255, 255));
        GutterBorder.Background = new SolidColorBrush(dark
            ? Windows.UI.Color.FromArgb(255, 35, 35, 39)
            : Windows.UI.Color.FromArgb(255, 247, 247, 248));
        ThemeIcon.Glyph = dark ? "\uE706" : "\uE708";
        _preview?.SetTheme(dark);
        if (!initial && _session is not null)
        {
            _ = RenderPreviewAsync(force: false);
            ScheduleHighlight();
        }
    }

    private void ApplyFontSize()
    {
        double size = Math.Clamp(_settings.EditorFontSize, 10, 28);
        Editor.FontSize = size;
        GutterText.FontSize = size;
        MeasureLineMetrics();
        UpdateGutter();
    }

    private void ApplyLayout(string layout, bool persist)
    {
        _settings.Layout = layout;
        switch (layout)
        {
            case "Editor":
                EditorColumn.Width = new GridLength(1, GridUnitType.Star);
                SplitterColumn.Width = new GridLength(0);
                PreviewColumn.Width = new GridLength(0);
                Splitter.Visibility = Visibility.Collapsed;
                LayoutLabel.Text = "Editor only";
                break;
            case "Preview":
                EditorColumn.Width = new GridLength(0);
                SplitterColumn.Width = new GridLength(0);
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                Splitter.Visibility = Visibility.Collapsed;
                LayoutLabel.Text = "Preview only";
                break;
            default:
                EditorColumn.Width = new GridLength(_splitRatio, GridUnitType.Star);
                SplitterColumn.Width = new GridLength(7);
                PreviewColumn.Width = new GridLength(1 - _splitRatio, GridUnitType.Star);
                Splitter.Visibility = Visibility.Visible;
                LayoutLabel.Text = "Split";
                break;
        }
        if (persist) QueueSettingsSave();
    }

    private void ApplyWindowGeometry()
    {
        var w = _settings.Window;
        try
        {
            AppWindow.MoveAndResize(new RectInt32(w.X, w.Y, Math.Max(900, w.Width), Math.Max(600, w.Height)));
            if (w.Maximized && AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not apply window geometry", ex);
        }
    }

    private void SaveSettingsNow()
    {
        try
        {
            _settings.EditorFontSize = Editor.FontSize;
            _settings.Layout = LayoutLabel.Text switch
            {
                "Editor only" => "Editor",
                "Preview only" => "Preview",
                _ => "Split"
            };
            _settings.SplitterRatio = _splitRatio;
            var pos = AppWindow.Position;
            var size = AppWindow.Size;
            bool maximized = AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
            _settings.Window = new WindowSettings
            {
                X = maximized ? _settings.Window.X : pos.X,
                Y = maximized ? _settings.Window.Y : pos.Y,
                Width = maximized ? _settings.Window.Width : size.Width,
                Height = maximized ? _settings.Window.Height : size.Height,
                Maximized = maximized
            };
            _ = _store.SaveAsync(_settings);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Settings save failed", ex);
        }
    }

    private void QueueSettingsSave()
    {
        _settingsTimer.Stop();
        _settingsTimer.Start();
    }

    private void OnWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange || args.DidPositionChange) QueueSettingsSave();
    }

    // ---------- Opening ----------

    private async void OnOpenClick(object sender, RoutedEventArgs e) => await PickAndOpenAsync();

    private async Task PickAndOpenAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".markdown");
        picker.FileTypeFilter.Add(".txt");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null) return;
        await OpenFileAsync(file.Path, checkDirty: true);
    }

    private async Task OpenFileAsync(string path, bool checkDirty)
    {
        if (checkDirty && !await ConfirmDiscardAsync()) return;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var loaded = await DocumentSession.LoadAsync(path);
            AttachSession(loaded);
            AppLogger.Info($"Opened {Path.GetFileName(path)} ({loaded.Text.Length} chars) in {stopwatch.ElapsedMilliseconds} ms");
            if (loaded.HadDecodeErrors)
            {
                ShowInfo(InfoBarSeverity.Warning, "Encoding warning",
                    "Some characters could not be decoded. Saving will rewrite the file with replacement characters.");
            }
            else
            {
                Info.IsOpen = false;
            }
        }
        catch (FileTooLargeException ex)
        {
            var result = await ShowLargeFileDialogAsync(ex);
            if (!result) return;
            try
            {
                var session = await DocumentSession.LoadAsync(path);
                AttachSession(session, readOnlyOverride: true);
            }
            catch (Exception inner)
            {
                AppLogger.Error($"Open failed: {path}", inner);
                ShowInfo(InfoBarSeverity.Error, "Could not open file", inner.Message);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Open failed: {path}", ex);
            ShowInfo(InfoBarSeverity.Error, "Could not open file", ex.Message);
        }
    }

    private void AttachSession(DocumentSession session, bool readOnlyOverride = false)
    {
        _session = session;
        _lastWriteSeen = DateTime.UtcNow;
        if (readOnlyOverride) { /* read-only guard is enforced by the dialog before attach */ }

        _suppressEditor = true;
        Editor.Document.SetText(TextSetOptions.None, session.Text.Replace("\n", "\r"));
        _suppressEditor = false;
        _editorTextCache = session.Text;

        StartSurface.Visibility = Visibility.Collapsed;
        DocumentGrid.Visibility = Visibility.Visible;
        UndoButton.IsEnabled = true;
        RedoButton.IsEnabled = true;
        UpdateCommandStates();
        UpdateTitle();
        UpdateStatus();
        UpdateWordCount();
        UpdateGutter();

        _settings.RecentFiles = RecentFiles.Add(_settings.RecentFiles, session.FilePath ?? session.FileName);
        QueueSettingsSave();
        RefreshRecentList();

        WatchFile(session.FilePath);
        _ = RenderPreviewAsync(force: true);
        ScheduleHighlight();
        Editor.Focus(FocusState.Programmatic);
        ToastText.Text = session.IsReadOnly ? "Opened read-only" : "Opened";
    }

    private async Task<bool> ShowLargeFileDialogAsync(FileTooLargeException ex)
    {
        var dialog = new ContentDialog
        {
            Title = "Large file",
            Content = $"This file is {ex.Size / 1024 / 1024.0:0.0} MB. Opening files over {ex.Limit / 1024 / 1024} MB may be slow. Open read-only?",
            PrimaryButtonText = "Open read-only",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootGrid.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var items = await e.DataView.GetStorageItemsAsync();
        var file = items.OfType<StorageFile>().FirstOrDefault();
        if (file is null) return;
        string ext = Path.GetExtension(file.Name).ToLowerInvariant();
        if (ext is not (".md" or ".markdown" or ".txt"))
        {
            ShowInfo(InfoBarSeverity.Informational, "Unsupported file",
                "Lumen Markdown opens .md, .markdown and .txt files.");
            return;
        }
        await OpenFileAsync(file.Path, checkDirty: true);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Open in Lumen Markdown";
        }
    }

    private void OnRecentItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecentFileItem item) _ = OpenFileAsync(item.Path, checkDirty: true);
    }

    private void RefreshRecentList()
    {
        _settings.RecentFiles = RecentFiles.Prune(_settings.RecentFiles);
        var items = _settings.RecentFiles.Select(p => new RecentFileItem(
            p,
            Path.GetFileName(p),
            Path.GetDirectoryName(p) ?? string.Empty)).ToList();
        RecentList.ItemsSource = items;
        NoRecentText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- Editing ----------

    private void OnEditorTextChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEditor || _session is null) return;
        Editor.Document.GetText(TextGetOptions.None, out string raw);
        if (raw.EndsWith('\r')) raw = raw[..^1];
        string text = raw.Replace("\r", "\n");
        _editorTextCache = text;
        _session.UpdateText(text);
        UpdateTitle();
        UpdateStatus();
        _wordTimer.Stop();
        _wordTimer.Start();
        _renderTimer.Stop();
        _renderTimer.Start();
        ScheduleHighlight();
        UpdateCommandStates();
    }

    private void ScheduleHighlight()
    {
        _highlightTimer.Stop();
        _highlightTimer.Start();
    }

    private void HighlightTick(object? sender, object e)
    {
        if (_session is null) return;
        try
        {
            var tokens = MarkdownTokenizer.Tokenize(_editorTextCache);
            _suppressEditor = true;
            EditorHighlighter.Apply(Editor, tokens, _dark);
            _suppressEditor = false;
        }
        catch (Exception ex)
        {
            _suppressEditor = false;
            AppLogger.Error("Highlighting failed", ex);
        }
    }

    private async void RenderTick(object? sender, object e) => await RenderPreviewAsync(force: false);

    private async Task RenderPreviewAsync(bool force)
    {
        if (_session is null || _preview is null || !_previewReady) return;
        int generation = ++_renderGeneration;
        string text = _session.Text;
        string? path = _session.FilePath;
        try
        {
            var sw = Stopwatch.StartNew();
            string fragment = await Task.Run(() => MarkdownRenderer.Render(text));
            if (generation != _renderGeneration) return;
            await _preview.RenderAsync(fragment, path, _dark, force);
            if (force) AppLogger.Info($"Preview rendered in {sw.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Preview render failed", ex);
            ShowInfo(InfoBarSeverity.Warning, "Preview could not update", ex.Message);
        }
    }

    private void OnEditorSelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateCaret();
        UpdateCommandStates();
    }

    private void UpdateCaret()
    {
        try
        {
            int index = Math.Max(0, Editor.Document.Selection.StartPosition);
            var range = Editor.Document.GetRange(0, index);
            range.GetText(TextGetOptions.None, out string before);
            int line = 1;
            int lastBreak = -1;
            for (int i = 0; i < before.Length; i++)
            {
                if (before[i] == '\r') { line++; lastBreak = i; }
            }
            int col = index - lastBreak;
            _lastCaretLine = line;
            CaretText.Text = $"Ln {line}, Col {col}";
        }
        catch
        {
            CaretText.Text = string.Empty;
        }
    }

    private void UpdateWordCount()
    {
        if (_session is null) { WordText.Text = string.Empty; return; }
        int words = _session.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        WordText.Text = $"{words} words";
    }

    private void UpdateTitle()
    {
        if (_session is null)
        {
            TitleText.Text = "Lumen Markdown";
            SubtitleText.Text = string.Empty;
            return;
        }
        string name = _session.FileName;
        TitleText.Text = "Lumen Markdown";
        SubtitleText.Text = name + (_session.IsDirty ? " •" : string.Empty);
    }

    private void UpdateStatus()
    {
        if (_session is null)
        {
            DirtyText.Text = "No document";
            EncodingText.Text = string.Empty;
            return;
        }
        DirtyText.Text = _session.IsDirty ? "● Unsaved" : (_session.IsReadOnly ? "Read-only" : "Saved");
        string eol = _session.LineEnding == "\r\n" ? "CRLF" : "LF";
        EncodingText.Text = $"{_session.Encoding.WebName.ToUpperInvariant()} · {eol}";
    }

    private void UpdateCommandStates()
    {
        bool has = _session is not null;
        SaveButton.IsEnabled = has && !_session!.IsReadOnly;
        SaveAsButton.IsEnabled = has;
        FindButton.IsEnabled = has;
        GotoButton.IsEnabled = has;
        UndoButton.IsEnabled = has;
        RedoButton.IsEnabled = has;
    }

    // ---------- Saving ----------

    private async void OnSaveClick(object sender, RoutedEventArgs e) => await SaveAsync(forcePicker: false);
    private async void OnSaveAsClick(object sender, RoutedEventArgs e) => await SaveAsync(forcePicker: true);

    private async Task<bool> SaveAsync(bool forcePicker)
    {
        if (_session is null) return false;
        string? path = _session.FilePath;

        if (forcePicker || string.IsNullOrEmpty(path))
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = Path.GetFileNameWithoutExtension(_session.FileName)
            };
            picker.FileTypeChoices.Add("Markdown", new List<string> { ".md" });
            picker.FileTypeChoices.Add("Text", new List<string> { ".txt" });
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            StorageFile? file = await picker.PickSaveFileAsync();
            if (file is null) return false;
            path = file.Path;
        }

        try
        {
            await TextFileWriter.SaveAsync(path!, _session.Text, _session.Encoding, _session.LineEnding, _session.HasBom);
            _session.MarkSaved(path!, _session.Encoding, _session.LineEnding, _session.HasBom);
            _lastWriteSeen = DateTime.UtcNow;
            _settings.RecentFiles = RecentFiles.Add(_settings.RecentFiles, path!);
            QueueSettingsSave();
            RefreshRecentList();
            DeleteDraft();
            WatchFile(path);
            UpdateTitle();
            UpdateStatus();
            UpdateCommandStates();
            ToastText.Text = $"Saved {Path.GetFileName(path)}";
            Info.IsOpen = false;
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Save failed: {path}", ex);
            ShowInfo(InfoBarSeverity.Error, "Could not save file", ex.Message);
            return false;
        }
    }

    // ---------- Find and go to line ----------

    private void OnFindClick(object sender, RoutedEventArgs e) => OpenFindBar();

    private void OpenFindBar()
    {
        if (_session is null) return;
        FindBar.Visibility = Visibility.Visible;
        FindBox.Focus(FocusState.Programmatic);
        FindBox.SelectAll();
        RecomputeMatches();
    }

    private void OnFindCloseClick(object sender, RoutedEventArgs e) => CloseFindBar();

    private void CloseFindBar()
    {
        FindBar.Visibility = Visibility.Collapsed;
        FindCount.Text = string.Empty;
        Editor.Focus(FocusState.Programmatic);
    }

    private void OnFindTextChanged(object sender, TextChangedEventArgs e) => RecomputeMatches();

    private void RecomputeMatches()
    {
        _matches.Clear();
        _matchIndex = 0;
        string query = FindBox.Text;
        if (string.IsNullOrEmpty(query) || _session is null)
        {
            FindCount.Text = string.Empty;
            return;
        }
        var comparison = MatchCaseToggle.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int index = 0;
        while (true)
        {
            int found = _session.Text.IndexOf(query, index, comparison);
            if (found < 0) break;
            _matches.Add((found, query.Length));
            index = found + Math.Max(1, query.Length);
        }
        FindCount.Text = _matches.Count == 0 ? "No results" : $"{(_matches.Count == 0 ? 0 : 1)} of {_matches.Count}";
        if (_matches.Count > 0) SelectMatch(0);
    }

    private void SelectMatch(int index)
    {
        if (_matches.Count == 0) return;
        _matchIndex = ((index % _matches.Count) + _matches.Count) % _matches.Count;
        var (start, length) = _matches[_matchIndex];
        Editor.Document.Selection.SetRange(start, start + length);
        Editor.Focus(FocusState.Programmatic);
        FindCount.Text = $"{_matchIndex + 1} of {_matches.Count}";
    }

    private void OnFindNextClick(object sender, RoutedEventArgs e) => SelectMatch(_matchIndex + 1);
    private void OnFindPreviousClick(object sender, RoutedEventArgs e) => SelectMatch(_matchIndex - 1);

    private void OnFindBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (IsShiftDown()) SelectMatch(_matchIndex - 1); else SelectMatch(_matchIndex + 1);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            CloseFindBar();
            e.Handled = true;
        }
    }

    private static bool IsShiftDown() =>
        (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

    private async void OnGotoClick(object sender, RoutedEventArgs e) => await ShowGotoDialogAsync();

    private async Task ShowGotoDialogAsync()
    {
        if (_session is null) return;
        var input = new TextBox { PlaceholderText = "Line number", Text = _lastCaretLine.ToString() };
        var dialog = new ContentDialog
        {
            Title = "Go to line",
            Content = input,
            PrimaryButtonText = "Go",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootGrid.XamlRoot
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (!int.TryParse(input.Text, out int line) || line < 1 || line > CountLines(_session.Text))
            {
                args.Cancel = true;
                input.Header = $"Enter a line between 1 and {CountLines(_session.Text)}";
            }
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (!int.TryParse(input.Text, out int target)) return;
        target = Math.Clamp(target, 1, CountLines(_session.Text));
        int index = 0;
        for (int i = 1; i < target; i++)
        {
            int next = _session.Text.IndexOf('\n', index);
            if (next < 0) break;
            index = next + 1;
        }
        Editor.Document.Selection.SetRange(index, index);
        Editor.Focus(FocusState.Programmatic);
    }

    private static int CountLines(string text)
    {
        int count = 1;
        foreach (char c in text) if (c == '\n') count++;
        return count;
    }

    // ---------- Undo / redo ----------

    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        Editor.Document.Undo();
        Editor.Focus(FocusState.Programmatic);
    }

    private void OnRedoClick(object sender, RoutedEventArgs e)
    {
        Editor.Document.Redo();
        Editor.Focus(FocusState.Programmatic);
    }

    // ---------- Gutter and scroll ----------

    private void OnEditorLoaded(object sender, RoutedEventArgs e)
    {
        _editorScroll = FindDescendant<ScrollViewer>(Editor);
        if (_editorScroll is not null)
        {
            _editorScroll.ViewChanged += (_, _) =>
            {
                UpdateGutter();
                if (!_syncingScroll)
                {
                    double max = _editorScroll.ScrollableHeight;
                    if (max > 0) _preview?.ScrollTo(_editorScroll.VerticalOffset / max);
                }
            };
        }
        MeasureLineMetrics();
        UpdateGutter();
    }

    private void MeasureLineMetrics()
    {
        try
        {
            Editor.Document.GetText(TextGetOptions.None, out string text);
            int second = text.IndexOf('\r');
            var r0 = Editor.Document.GetRange(0, 0);
            r0.GetRect(PointOptions.None, out Rect first, out _);
            if (second >= 1)
            {
                var r1 = Editor.Document.GetRange(second + 1, second + 1);
                r1.GetRect(PointOptions.None, out Rect next, out _);
                if (next.Top > first.Top) _lineHeight = next.Top - first.Top;
            }
            _contentTop = first.Top + (_editorScroll?.VerticalOffset ?? 0);
        }
        catch
        {
            _lineHeight = Editor.FontSize * 1.45;
        }
    }

    private void UpdateGutter()
    {
        if (_session is null || _editorScroll is null) return;
        double offset = _editorScroll.VerticalOffset;
        int total = CountLines(_editorTextCache);
        int first = Math.Max(0, (int)Math.Floor(offset / Math.Max(1, _lineHeight)) - 1);
        int visible = (int)Math.Ceiling((GutterCanvas.ActualHeight > 0 ? GutterCanvas.ActualHeight : 600) / Math.Max(1, _lineHeight)) + 3;
        int last = Math.Min(total, first + visible);
        if (last <= first) return;

        var sb = new System.Text.StringBuilder();
        for (int i = first + 1; i <= last; i++)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(i);
        }
        GutterText.Text = sb.ToString();
        GutterText.LineHeight = _lineHeight;
        GutterText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        GutterTransform.Y = _contentTop + first * _lineHeight - offset;
    }

    private void SyncEditorScroll(double ratio)
    {
        if (_editorScroll is null) return;
        _syncingScroll = true;
        try
        {
            double max = _editorScroll.ScrollableHeight;
            _editorScroll.ChangeView(null, Math.Clamp(ratio, 0, 1) * max, null, disableAnimation: true);
        }
        finally
        {
            _syncingScroll = false;
        }
    }

    private void GutterCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        GutterCanvas.Clip = new RectangleGeometry { Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
        UpdateGutter();
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            var nested = FindDescendant<T>(child);
            if (nested is not null) return nested;
        }
        return null;
    }

    // ---------- Splitter ----------

    private bool _dragging;
    private double _dragStartX;

    private void OnSplitterPressed(object sender, PointerRoutedEventArgs e)
    {
        _dragging = true;
        _dragStartX = e.GetCurrentPoint(DocumentGrid).Position.X;
        Splitter.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnSplitterMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        double x = e.GetCurrentPoint(DocumentGrid).Position.X;
        double total = DocumentGrid.ActualWidth;
        if (total <= 0) return;
        double ratio = Math.Clamp(x / total, 0.2, 0.8);
        _splitRatio = ratio;
        EditorColumn.Width = new GridLength(ratio, GridUnitType.Star);
        PreviewColumn.Width = new GridLength(1 - ratio, GridUnitType.Star);
        e.Handled = true;
    }

    private void OnSplitterReleased(object sender, PointerRoutedEventArgs e)
    {
        _dragging = false;
        Splitter.ReleasePointerCapture(e.Pointer);
        QueueSettingsSave();
        e.Handled = true;
    }

    // ---------- External change, drafts ----------

    private void WatchFile(string? path)
    {
        _watcher?.Dispose();
        _watcher = null;
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (dir is null) return;
            _watcher = new FileSystemWatcher(dir, Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            _watcher.Changed += (_, _) => { _watchTimer.Stop(); _watchTimer.Start(); };
        }
        catch (Exception ex)
        {
            AppLogger.Error("File watcher could not start", ex);
        }
    }

    private void CheckExternalChange()
    {
        if (_session?.FilePath is null) return;
        try
        {
            var info = new FileInfo(_session.FilePath);
            if (!info.Exists || info.LastWriteTimeUtc <= _lastWriteSeen) return;
            ShowReloadBanner();
        }
        catch (Exception ex)
        {
            AppLogger.Error("External change check failed", ex);
        }
    }

    private void ShowReloadBanner()
    {
        Info.Severity = InfoBarSeverity.Warning;
        Info.Title = "File changed on disk";
        Info.Message = "This file was modified by another program.";
        var button = new Button { Content = "Reload" };
        button.Click += async (_, _) =>
        {
            Info.IsOpen = false;
            if (_session?.FilePath is not null) await OpenFileAsync(_session.FilePath, checkDirty: true);
        };
        Info.ActionButton = button;
        Info.IsOpen = true;
    }

    private void WriteDraft()
    {
        if (_session is null || !_session.IsDirty || string.IsNullOrEmpty(_session.Text)) return;
        try
        {
            Directory.CreateDirectory(AppPaths.DraftDirectory);
            File.WriteAllText(Path.Combine(AppPaths.DraftDirectory, "draft.md"), _session.Text);
            File.WriteAllText(Path.Combine(AppPaths.DraftDirectory, "draft.json"),
                JsonSerializer.Serialize(new { path = _session.FilePath, saved = DateTime.UtcNow }));
        }
        catch (Exception ex)
        {
            AppLogger.Error("Draft write failed", ex);
        }
    }

    private static void DeleteDraft()
    {
        try
        {
            string text = Path.Combine(AppPaths.DraftDirectory, "draft.md");
            string meta = Path.Combine(AppPaths.DraftDirectory, "draft.json");
            if (File.Exists(text)) File.Delete(text);
            if (File.Exists(meta)) File.Delete(meta);
        }
        catch
        {
            // best effort
        }
    }

    private async Task OfferDraftRecoveryAsync()
    {
        try
        {
            string draft = Path.Combine(AppPaths.DraftDirectory, "draft.md");
            if (!File.Exists(draft)) return;
            string text = await File.ReadAllTextAsync(draft);
            if (string.IsNullOrWhiteSpace(text)) return;

            var dialog = new ContentDialog
            {
                Title = "Recover unsaved work",
                Content = "Lumen Markdown found an unsaved draft from a previous session. Recover it?",
                PrimaryButtonText = "Recover",
                CloseButtonText = "Discard",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = RootGrid.XamlRoot
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                DeleteDraft();
                return;
            }

            var session = DocumentSession.CreateUntitled();
            session.UpdateText(text);
            AttachSession(session);
            ToastText.Text = "Recovered draft — use Save as to choose a file";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Draft recovery failed", ex);
        }
    }

    // ---------- Closing ----------

    private async void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_forceClose) return;
        if (_session is null || !_session.IsDirty)
        {
            SaveSettingsNow();
            return;
        }

        args.Cancel = true;
        var result = await ShowUnsavedDialogAsync();
        if (result == UnsavedResult.Cancel) return;

        if (result == UnsavedResult.Save)
        {
            bool saved = await SaveAsync(forcePicker: _session.FilePath is null);
            if (!saved) return;
        }

        DeleteDraft();
        _forceClose = true;
        Close();
    }

    private enum UnsavedResult { Save, Discard, Cancel }

    private async Task<UnsavedResult> ShowUnsavedDialogAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Unsaved changes",
            Content = $"Save changes to {_session?.FileName}?",
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Don't save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootGrid.XamlRoot
        };
        var result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => UnsavedResult.Save,
            ContentDialogResult.Secondary => UnsavedResult.Discard,
            _ => UnsavedResult.Cancel
        };
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        if (_session is null || !_session.IsDirty) return true;
        var result = await ShowUnsavedDialogAsync();
        if (result == UnsavedResult.Cancel) return false;
        if (result == UnsavedResult.Save) return await SaveAsync(forcePicker: _session.FilePath is null);
        return true;
    }

    // ---------- Theme, layout, info ----------

    private void OnThemeClick(object sender, RoutedEventArgs e)
    {
        _settings.Theme = _dark ? "Light" : "Dark";
        ApplyTheme();
        QueueSettingsSave();
    }

    private void OnLayoutSplitClick(object sender, RoutedEventArgs e) => ApplyLayout("Split", true);
    private void OnLayoutEditorClick(object sender, RoutedEventArgs e) => ApplyLayout("Editor", true);
    private void OnLayoutPreviewClick(object sender, RoutedEventArgs e) => ApplyLayout("Preview", true);

    private void ShowInfo(InfoBarSeverity severity, string title, string message)
    {
        Info.Severity = severity;
        Info.Title = title;
        Info.Message = message;
        Info.ActionButton = null;
        Info.IsOpen = true;
    }

    private async void OnAboutClick(object sender, RoutedEventArgs e) => await ShowAboutAsync();

    private async Task ShowAboutAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Lumen Markdown 1.0.0",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = "A local Markdown reader and editor for Windows 11.\n\n" +
                       "Ctrl+O  Open\nCtrl+S  Save\nCtrl+Shift+S  Save as\n" +
                       "Ctrl+F  Find\nCtrl+G  Go to line\nCtrl+Shift+P  Change layout\n" +
                       "Ctrl+Shift+L  Toggle theme\nF5  Reload from disk\nF1  About",
                LineHeight = 22
            },
            CloseButtonText = "Close",
            XamlRoot = RootGrid.XamlRoot
        };
        await dialog.ShowAsync();
    }

    // ---------- Accelerators ----------

    private async void OnOpenAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await PickAndOpenAsync();
    }

    private async void OnSaveAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await SaveAsync(forcePicker: false);
    }

    private async void OnSaveAsAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await SaveAsync(forcePicker: true);
    }

    private void OnFindAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        OpenFindBar();
    }

    private async void OnGotoAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowGotoDialogAsync();
    }

    private void OnLayoutAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        string next = LayoutLabel.Text switch
        {
            "Split" => "Editor",
            "Editor only" => "Preview",
            _ => "Split"
        };
        ApplyLayout(next, true);
    }

    private void OnThemeAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _settings.Theme = _dark ? "Light" : "Dark";
        ApplyTheme();
        QueueSettingsSave();
    }

    private async void OnCloseDocumentAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (_session is null) return;
        if (!await ConfirmDiscardAsync()) return;
        _session = null;
        _suppressEditor = true;
        Editor.Document.SetText(TextSetOptions.None, string.Empty);
        _suppressEditor = false;
        DocumentGrid.Visibility = Visibility.Collapsed;
        StartSurface.Visibility = Visibility.Visible;
        UpdateCommandStates();
        UpdateTitle();
        UpdateStatus();
        TitleText.Text = "Lumen Markdown";
        SubtitleText.Text = string.Empty;
        _watcher?.Dispose();
        _watcher = null;
    }

    private async void OnReloadAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (_session?.FilePath is null) return;
        await OpenFileAsync(_session.FilePath, checkDirty: true);
    }

    private async void OnAboutAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowAboutAsync();
    }
}

public sealed record RecentFileItem(string Path, string DisplayName, string Folder);
