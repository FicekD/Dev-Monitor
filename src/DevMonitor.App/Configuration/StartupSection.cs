using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DevMonitor.App.Controls;
using DevMonitor.Windows;
using Serilog;

namespace DevMonitor.App.Configuration;

public sealed class StartupSection : UserControl
{
    static readonly ILogger Log = Serilog.Log.ForContext<StartupSection>();

    readonly ToggleSwitch autostart = new() { OnContent = null, OffContent = null, IsEnabled = false };
    readonly TextBlock autostartState = new() { Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
    readonly Notice error = new() { IsCritical = true, IsVisible = false, Margin = new Thickness(0, 8, 0, 0) };
    bool updating;

    public StartupSection()
    {
        autostart.IsCheckedChanged += async (_, _) =>
        {
            if (updating)
                return;
            var enable = autostart.IsChecked == true;
            autostart.IsEnabled = false;
            error.IsVisible = false;
            try
            {
                await Task.Run(() =>
                {
                    if (enable)
                        Autostart.Enable(Environment.ProcessPath!);
                    else
                        Autostart.Disable();
                });
                Log.Information(enable ? "Startup task registered for {Executable}" : "Startup task removed", Environment.ProcessPath);
            }
            catch (InvalidOperationException exception)
            {
                Log.Warning("Changing the startup task failed: {Error}", exception.Message);
                error.Text = exception.Message;
                error.IsVisible = true;
            }
            await LoadAsync();
        };

        var startupRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        startupRow.Children.AddRange([autostart, autostartState]);

        var elevated = Elevation.IsElevated;
        var permissions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        permissions.Children.Add(new TextBlock
        {
            Text = elevated ? "Running as administrator" : "Running without administrator rights",
            Classes = { "muted" },
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (!elevated)
        {
            var restart = new Button { Content = "Restart as administrator" };
            restart.Click += (_, _) => ((App)Application.Current!).RestartElevated();
            permissions.Children.Add(restart);
        }

        var rows = new StackPanel { Classes = { "rows" } };
        rows.Children.Add(new FieldRow { Label = "Start with Windows", Content = startupRow });
        rows.Children.Add(new FieldRow { Label = "Permissions", Content = permissions });
        rows.Children.Add(error);
        Content = new Section { Title = "Startup", Content = rows };
        _ = LoadAsync();
    }

    async Task LoadAsync()
    {
        var registered = await Task.Run(Autostart.RegisteredExecutable);
        updating = true;
        autostart.IsChecked = registered is not null;
        updating = false;
        autostart.IsEnabled = true;
        autostartState.Text = registered switch
        {
            null => "",
            _ when string.Equals(registered, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase) =>
                "Starts in the tray at sign-in with administrator rights",
            _ => $"Registered for {registered}",
        };
    }
}
