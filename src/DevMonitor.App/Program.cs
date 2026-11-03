using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using DevMonitor.Core;
using DevMonitor.Windows;
using Serilog;
using Serilog.Events;

namespace DevMonitor.App;

static class Program
{
    const string InstanceName = "DevMonitor";

    public static string Version { get; } =
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    public static SingleInstance? Instance { get; set; }

    [STAThread]
    public static int Main(string[] args)
    {
#if DEBUG
        Logging.Configure(AppPaths.Logs, LogEventLevel.Debug);
#else
        Logging.Configure(AppPaths.Logs, LogEventLevel.Information);
#endif
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception, "Dev Monitor crashed");
            Log.CloseAndFlush();
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "A background task failed");
            e.SetObserved();
        };

        try
        {
            if (args.Contains("--enable-autostart"))
            {
                Autostart.Enable(Environment.ProcessPath!);
                Log.Information("Registered the startup task for {Executable}", Environment.ProcessPath);
                return 0;
            }
            if (args.Contains("--disable-autostart"))
            {
                Autostart.Disable();
                Log.Information("Removed the startup task");
                return 0;
            }
            if (args.Contains("--elevate") && !Elevation.IsElevated)
            {
                var forwarded = Elevation.Quote(args.Where(argument => argument != "--elevate"));
                if (Elevation.Start(Environment.ProcessPath!, forwarded) is not null)
                    return 0;
                Log.Warning("Administrator approval was declined, starting without it");
            }

            if (!TryAcquireInstance())
            {
                Log.Information("Dev Monitor is already running, asking it to open its window");
                return 0;
            }
            Log.Information("Dev Monitor {Version} starting, administrator: {Elevated}", Version, Elevation.IsElevated);
            Avalonia.Logging.Logger.Sink = new AvaloniaLogSink();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
            Log.Information("Dev Monitor stopped");
            return 0;
        }
        catch (InvalidOperationException exception)
        {
            Log.Error("{Error}", exception.Message);
            return 1;
        }
        finally
        {
            Instance?.Dispose();
            Log.CloseAndFlush();
        }
    }

    public static bool TryAcquireInstance()
    {
        Instance = SingleInstance.TryAcquire(InstanceName);
        return Instance is not null;
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}
