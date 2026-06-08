using Microsoft.UI.Xaml;
using System.Text;

namespace PromptInputMethod.App;

public partial class App : Application
{
    private CompactPromptWindow? _window;

    public App()
    {
        try
        {
            InitializeComponent();
            UnhandledException += App_UnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }
        catch (Exception ex)
        {
            LogStartupException(ex, "App constructor");
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new CompactPromptWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            LogStartupException(ex, "OnLaunched");
            throw;
        }
    }

    private static void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogStartupException(e.Exception, "WinUI unhandled exception");
        e.Handled = true;
    }

    private static void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        LogStartupException(e.ExceptionObject as Exception, "AppDomain unhandled exception");
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogStartupException(e.Exception, "TaskScheduler unobserved exception");
        e.SetObserved();
    }

    private static void LogStartupException(Exception? ex, string source)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDirectory = Path.Combine(appData, "PromptInputMethod", "logs");
            Directory.CreateDirectory(logDirectory);

            var builder = new StringBuilder()
                .Append('[')
                .Append(DateTimeOffset.Now.ToString("O"))
                .Append("] ")
                .AppendLine(source)
                .Append("OS: ")
                .AppendLine(Environment.OSVersion.VersionString)
                .Append("AppBase: ")
                .AppendLine(AppContext.BaseDirectory)
                .AppendLine(ex?.ToString() ?? "No exception object was provided.")
                .AppendLine();

            File.AppendAllText(Path.Combine(logDirectory, "startup.log"), builder.ToString());
        }
        catch
        {
            // Startup logging must never become another launch failure.
        }
    }
}
