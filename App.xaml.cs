using System.Windows;
using Luba.Core;

namespace Luba;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (s, a) =>
        {
            LogError(a.Exception);
            MessageBox.Show("Ошибка лаунчера:\n" + a.Exception.Message + "\n\nПодробности: " + Paths.Logs,
                "AZURE", MessageBoxButton.OK, MessageBoxImage.Error);
            a.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, a) => LogError(a.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (s, a) => { LogError(a.Exception); a.SetObserved(); };
    }

    public static void LogError(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Paths.Logs);
            File.AppendAllText(Path.Combine(Paths.Logs, "launcher-errors.log"), $"[{DateTime.Now:u}] {ex}\n\n");
        }
        catch { }
    }
}
