using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DevMonitor.App.Controls;
using DevMonitor.App.Shell;
using DevMonitor.Core;
using DevMonitor.Core.GitHub;

namespace DevMonitor.App.Configuration;

public sealed class SettingsPage : UserControl
{
    public SettingsPage(AppServices services) =>
        Content = Pages.Layout("Settings", null,
            Appearance(services),
            new StartupSection(),
            GitHubToken(services),
            new RecordedSensorsView(services),
            About());

    static readonly int[] Scales = [50, 75, 100, 125, 150];

    static Section Appearance(AppServices services)
    {
        var scale = new ComboBox
        {
            Width = 120,
            ItemsSource = Scales.Select(value => $"{value}%").ToList(),
            SelectedIndex = Math.Max(0, Array.IndexOf(Scales, services.Settings.UiScale)),
        };
        scale.SelectionChanged += (_, _) =>
            services.UpdateSettings(settings => settings with { UiScale = Scales[scale.SelectedIndex] });

        var theme = new ListBox
        {
            Classes = { "segmented" },
            SelectionMode = SelectionMode.Single | SelectionMode.AlwaysSelected,
            ItemsSource = Enum.GetValues<ThemePreference>(),
            SelectedItem = services.Settings.Theme,
        };
        theme.SelectionChanged += (_, _) =>
            services.UpdateSettings(settings => settings with { Theme = (ThemePreference)theme.SelectedItem! });
        var rows = new StackPanel { Classes = { "rows" } };
        rows.Children.Add(new FieldRow { Label = "Theme", Content = theme });
        rows.Children.Add(new FieldRow { Label = "Scale", Content = scale });
        return new Section { Title = "Appearance", Content = rows };
    }

    static Section GitHubToken(AppServices services)
    {
        var state = new TextBlock { Classes = { "muted" } };
        void ShowState() => state.Text = services.Secrets.Get(GitHubActionsClient.TokenSecretName) is null
            ? "No token is stored."
            : "A token is stored in Windows Credential Manager.";
        ShowState();

        var token = new TextBox { Width = 340, PasswordChar = '•', Watermark = "Fine-grained token with Plan read access" };
        var save = new Button { Content = "Save token", Classes = { "accent" }, IsEnabled = false };
        var remove = new Button { Content = "Remove token" };
        token.TextChanged += (_, _) => save.IsEnabled = !string.IsNullOrWhiteSpace(token.Text);
        save.Click += (_, _) =>
        {
            services.Secrets.Set(GitHubActionsClient.TokenSecretName, token.Text!.Trim());
            token.Text = "";
            ShowState();
            services.RefreshGitHub();
        };
        remove.Click += (_, _) =>
        {
            services.Secrets.Delete(GitHubActionsClient.TokenSecretName);
            ShowState();
        };

        var input = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        input.Children.AddRange([token, save, remove]);
        var rows = new StackPanel { Classes = { "rows" } };
        rows.Children.Add(new FieldRow { Label = "Status", Content = state });
        rows.Children.Add(new FieldRow { Label = "Token", Content = input });
        return new Section { Title = "GitHub", Content = rows };
    }

    static Section About()
    {
        var open = new Button { Content = "Open folder" };
        open.Click += (_, _) =>
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            Process.Start(new ProcessStartInfo(AppPaths.DataDirectory) { UseShellExecute = true });
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        row.Children.Add(new TextBlock { Text = AppPaths.DataDirectory, Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(open);
        var rows = new StackPanel { Classes = { "rows" } };
        rows.Children.Add(new FieldRow { Label = "Version", Content = new TextBlock { Text = Program.Version, Classes = { "muted" } } });
        rows.Children.Add(new FieldRow { Label = "Data folder", Content = row });
        return new Section { Title = "About", Content = rows };
    }
}
