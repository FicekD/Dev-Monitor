using Avalonia.Controls;
using DevMonitor.App.Claude;
using DevMonitor.App.GitHub;
using DevMonitor.App.Hardware;
using DevMonitor.App.Shell;

namespace DevMonitor.App.Overview;

public sealed class OverviewPage : UserControl
{
    public OverviewPage(AppServices services) =>
        Content = Pages.Layout("Overview", null,
            new ClaudeLimitsView(services, "Claude"),
            new ActionsBudgetView(services, "GitHub Actions", detailed: false),
            new TemperaturesView(services));
}
