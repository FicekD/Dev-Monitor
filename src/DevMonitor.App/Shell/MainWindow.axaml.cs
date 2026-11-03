using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DevMonitor.App.Controls;
using DevMonitor.Windows;

namespace DevMonitor.App.Shell;

public partial class MainWindow : Window
{
    const double BaseMinWidth = 820;
    const double BaseMinHeight = 520;

    readonly AppServices services;
    readonly Dictionary<string, NavButton> buttons = [];

    public MainWindow(AppServices services)
    {
        this.services = services;
        InitializeComponent();
        ApplyScale();
        services.SettingsChanged += ApplyScale;
        Closed += (_, _) => services.SettingsChanged -= ApplyScale;
        RestorePlacement();

        MinimizeButton.Click += (_, _) => WindowState = WindowState.Minimized;
        MaximizeButton.Click += (_, _) => ToggleMaximized();
        CloseButton.Click += (_, _) => Close();
        TitleBar.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;
            if (e.ClickCount == 2)
                ToggleMaximized();
            else
                BeginMoveDrag(e);
        };
        AddNavigation(MainNav, Pages.Main);
        AddNavigation(FooterNav, Pages.Footer);

        var pages = Pages.Main.Concat(Pages.Footer).ToList();
        for (var index = 0; index < pages.Count; index++)
        {
            var page = pages[index];
            KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.D1 + index, KeyModifiers.Control),
                Command = new NavigateCommand(() => buttons[page.Key].IsChecked = true),
            });
        }

        var start = pages.FirstOrDefault(page => page.Key == services.Ui.Page) ?? pages[0];
        buttons[start.Key].IsChecked = true;
    }

    void AddNavigation(Panel panel, IReadOnlyList<PageDefinition> pages)
    {
        foreach (var page in pages)
        {
            var button = new NavButton
            {
                Content = page.Title,
                Icon = this.FindResource(page.IconKey) as Geometry,
                GroupName = "navigation",
            };
            button.IsCheckedChanged += (_, _) =>
            {
                if (button.IsChecked != true)
                    return;
                services.Ui.Page = page.Key;
                Host.Content = page.Create(services);
            };
            buttons[page.Key] = button;
            panel.Children.Add(button);
        }
    }

    void ToggleMaximized() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    void ApplyScale()
    {
        var scale = services.Settings.UiScale / 100.0;
        Scaler.LayoutTransform = new ScaleTransform(scale, scale);
        MinWidth = BaseMinWidth * Math.Min(scale, 1);
        MinHeight = BaseMinHeight * Math.Min(scale, 1);
    }

    void RestorePlacement()
    {
        var ui = services.Ui;
        if (ui.Width is { } width && ui.Height is { } height)
        {
            Width = Math.Max(width, MinWidth);
            Height = Math.Max(height, MinHeight);
        }
        if (ui.X is { } x && ui.Y is { } y
            && Screens.All.Any(screen => screen.WorkingArea.Contains(new PixelPoint(x + 40, y + 20))))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(x, y);
        }
        if (ui.Maximized)
            WindowState = WindowState.Maximized;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == WindowStateProperty || change.Property == OffScreenMarginProperty) && MaximizeButton is not null)
        {
            var maximized = WindowState == WindowState.Maximized;
            Padding = maximized ? OffScreenMargin : default;
            MaximizeButton.Icon = this.FindResource(maximized ? "IconRestore" : "IconMaximize") as Geometry;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        var ui = services.Ui;
        ui.Maximized = WindowState == WindowState.Maximized;
        if (TryGetPlatformHandle()?.Handle is { } handle && WindowPlacement.NormalBounds(handle) is var (x, y, width, height))
        {
            (ui.X, ui.Y) = (x, y);
            ui.Width = width / DesktopScaling;
            ui.Height = height / DesktopScaling;
        }
        ui.Save();
    }

    sealed class NavigateCommand(Action action) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
    }
}
