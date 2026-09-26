using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace OpenServerOps;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) => { WriteCrash(e.Exception); e.Handled = true; System.Windows.MessageBox.Show("OpenServerOps encountered an error. A diagnostic was saved to the temporary folder.", "OpenServerOps", MessageBoxButton.OK, MessageBoxImage.Error); };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => WriteCrash(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { WriteCrash(e.Exception); e.SetObserved(); };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        var window = new MainWindow();
        MainWindow = window;
        window.Closed += (_, _) => WriteCrash(new InvalidOperationException("Main window closed; shutdown mode=" + ShutdownMode));
        Exit += (_, _) => WriteCrash(new InvalidOperationException("Application exited."));
        window.Show();
        window.Activate();
    }

    private static void WriteCrash(Exception? exception)
    {
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "OpenServerOps");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "startup-errors.log"), $"[{DateTime.Now:O}] {exception}\n\n");
        }
        catch { }
    }
}
