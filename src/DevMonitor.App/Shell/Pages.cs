using Avalonia.Controls;
using Avalonia.Layout;
using DevMonitor.App.Claude;
using DevMonitor.App.GitHub;
using DevMonitor.App.Hardware;
using DevMonitor.App.Dashboard;
using DevMonitor.App.Configuration;

namespace DevMonitor.App.Shell;

public sealed record PageDefinition(string Key, string Title, string IconKey, Func<AppServices, Control> Create);

public static class Pages
{
    public static readonly IReadOnlyList<PageDefinition> Main =
    [
        new("dashboard", "Dashboard", "IconDashboard", services => new DashboardPage(services)),
        new("claude", "Claude", "IconClaude", services => new ClaudePage(services)),
        new("github", "GitHub Actions", "IconActions", services => new GitHubPage(services)),
        new("hardware", "Hardware", "IconHardware", services => new HardwarePage(services)),
    ];

    public static readonly IReadOnlyList<PageDefinition> Footer =
    [
        new("settings", "Settings", "IconSettings", services => new SettingsPage(services)),
    ];

    public static Control Layout(string title, Control? actions, params Control[] sections) =>
        Layout(title, actions, false, sections);

    public static Control Layout(string title, Control? actions, bool wide, params Control[] sections)
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), MinHeight = 36 };
        header.Children.Add(new TextBlock { Text = title, Classes = { "title" }, VerticalAlignment = VerticalAlignment.Center });
        if (actions is not null)
        {
            Grid.SetColumn(actions, 1);
            actions.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(actions);
        }
        var page = new StackPanel { Classes = { "page" } };
        if (wide)
        {
            page.MaxWidth = double.PositiveInfinity;
            page.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        page.Children.Add(header);
        page.Children.AddRange(sections);
        return new ScrollViewer { Content = page, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }
}
