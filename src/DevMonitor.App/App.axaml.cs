using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using DevMonitor.App.Shell;
using DevMonitor.Core;
using DevMonitor.Windows;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Serilog;
using SkiaSharp;

namespace DevMonitor.App;

public partial class App : Application
{
    AppServices? services;
    MainWindow? window;
    TrayIcon? tray;
    IClassicDesktopStyleApplicationLifetime? lifetime;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            lifetime = desktop;
            using (var font = AssetLoader.Open(new Uri("avares://DevMonitor/Assets/Fonts/Barlow-Medium.ttf")))
            {
                var typeface = SKTypeface.FromStream(font);
                LiveCharts.Configure(config => config.HasTextSettings(new TextSettings { DefaultTypeface = typeface }));
            }

            services = new AppServices();
            ApplyTheme();
            services.SettingsChanged += ApplyTheme;
            CreateTray(desktop);
            services.Collector.Changed += () => Dispatcher.UIThread.Post(UpdateTray);
            WatchInstance();
            desktop.Exit += (_, _) => services.Dispose();

            if (!(desktop.Args ?? []).Contains("--tray"))
                OpenWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }

    void WatchInstance() => Program.Instance?.Activated += () => Dispatcher.UIThread.Post(OpenWindow);

    public bool RestartElevated()
    {
        Program.Instance?.Dispose();
        Program.Instance = null;
        if (Elevation.Start(Environment.ProcessPath!, "") is null)
        {
            Log.Information("Restarting as administrator was declined");
            if (Program.TryAcquireInstance())
                WatchInstance();
            return false;
        }
        Log.Information("Restarting as administrator");
        lifetime!.Shutdown();
        return true;
    }

    void ApplyTheme() => RequestedThemeVariant = services!.Settings.Theme switch
    {
        ThemePreference.Light => ThemeVariant.Light,
        ThemePreference.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    void CreateTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var open = new NativeMenuItem("Open Dev Monitor");
        open.Click += (_, _) => OpenWindow();
        var exit = new NativeMenuItem("Exit");
        exit.Click += (_, _) => desktop.Shutdown();

        tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://DevMonitor/Assets/Icon.ico"))),
            ToolTipText = "Dev Monitor",
            Menu = new NativeMenu { Items = { open, new NativeMenuItemSeparator(), exit } },
        };
        tray.Clicked += (_, _) => OpenWindow();
        TrayIcon.SetIcons(this, [tray]);
    }

    void UpdateTray()
    {
        var now = DateTimeOffset.UtcNow;
        var claude = services!.Collector.Claude;
        var text = claude.Value?.FiveHour is { Utilization: { } used } window && !claude.IsStale(now)
            ? $"Claude 5-hour limit {used:0}% used, {Display.ResetsIn(window.ResetsAt, now)}"
            : "Dev Monitor";
        if (tray!.ToolTipText != text)
            tray.ToolTipText = text;
    }

    void OpenWindow()
    {
        if (window is not null)
        {
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = services!.Ui.Maximized ? WindowState.Maximized : WindowState.Normal;
            window.Activate();
            return;
        }
        window = new MainWindow(services!);
        window.Closed += (_, _) => window = null;
        window.Show();
        window.Activate();
    }
}
