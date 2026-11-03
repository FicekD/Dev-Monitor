using Avalonia;
using Avalonia.Controls;
using DevMonitor.Windows;

namespace DevMonitor.App;

static class Program
{
    public static SingleInstance? Instance { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        Instance = SingleInstance.TryAcquire("DevMonitor");
        if (Instance is null)
            return;
        using (Instance)
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
