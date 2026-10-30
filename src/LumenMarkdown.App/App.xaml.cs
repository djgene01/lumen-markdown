using LumenMarkdown.Core.Logging;

namespace LumenMarkdown.App;

public partial class App : Application
{
    public static MainWindow? Window { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        AppLogger.Error("Unhandled UI exception", e.Exception);
        e.Handled = true;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var startup = System.Diagnostics.Stopwatch.StartNew();
        AppPaths.EnsureCreated();
        AppLogger.Initialize(AppPaths.LogDirectory);
        AppLogger.Info("Application starting.");

        Window = new MainWindow();
        Window.Activate();
        AppLogger.Info($"Window activated in {startup.ElapsedMilliseconds} ms");

        string? initial = null;
        var commandLine = Environment.GetCommandLineArgs();
        if (commandLine.Length > 1 && !commandLine[1].StartsWith('-'))
        {
            initial = commandLine[1];
        }

        if (!string.IsNullOrWhiteSpace(initial))
        {
            Window.LoadInitialFile(initial!);
        }
    }
}
