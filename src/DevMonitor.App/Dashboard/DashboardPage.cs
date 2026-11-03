using Avalonia;
using Avalonia.Controls;
using DevMonitor.App.Claude;
using DevMonitor.App.GitHub;
using DevMonitor.App.Hardware;
using DevMonitor.App.Shell;

namespace DevMonitor.App.Dashboard;

public sealed class DashboardPage : UserControl
{
    const double TwoColumnWidth = 1180;
    const double ColumnGap = 48;
    const double SectionGap = 36;

    public DashboardPage(AppServices services)
    {
        var usage = new StackPanel { Spacing = SectionGap };
        usage.Children.Add(new ClaudeLimitsView(services, "Claude"));
        usage.Children.Add(new ActionsBudgetView(services, "GitHub Actions", detailed: false));
        var temperatures = new TemperaturesView(services);

        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,0"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        columns.Children.AddRange([usage, temperatures]);
        columns.PropertyChanged += (_, change) =>
        {
            if (change.Property != BoundsProperty)
                return;
            var wide = columns.Bounds.Width >= TwoColumnWidth;
            columns.ColumnDefinitions[1].Width = wide ? GridLength.Star : new GridLength(0);
            Grid.SetColumn(temperatures, wide ? 1 : 0);
            Grid.SetRow(temperatures, wide ? 0 : 1);
            temperatures.Margin = wide ? new Thickness(ColumnGap, 0, 0, 0) : new Thickness(0, SectionGap, 0, 0);
        };
        Grid.SetRow(temperatures, 1);
        temperatures.Margin = new Thickness(0, SectionGap, 0, 0);

        Content = Pages.Layout("Dashboard", null, wide: true, columns);
    }
}
