using Avalonia.Controls;
using DevMonitor.App.Controls;
using DevMonitor.App.Shell;
using DevMonitor.Core.Storage;

namespace DevMonitor.App.GitHub;

public sealed class GitHubPage : UserControl
{
    public GitHubPage(AppServices services) =>
        Content = Pages.Layout("GitHub Actions", null,
            new ActionsBudgetView(services, "Usage", detailed: true),
            new ActionsHistoryView(services));
}

public sealed class ActionsHistoryView : LiveView
{
    readonly HistoryChart chart;
    (DateTimeOffset From, DateTimeOffset To)? zoom;

    public ActionsHistoryView(AppServices services) : base(services)
    {
        chart = new HistoryChart(services.Ui.HiddenSeries);
        chart.ZoomRequested += (from, to) =>
        {
            zoom = (from, to);
            Reload();
        };
        chart.ZoomReset += () =>
        {
            zoom = null;
            Reload();
        };
        Content = new Section { Title = "This month", Content = chart };
    }

    protected override TimeSpan ReloadInterval => TimeSpan.FromMinutes(10);

    protected override void Refresh(DateTimeOffset now)
    {
        if (Services.Collector.GitHub.Value is { } usage)
            chart.SetValue("github/spent", Display.Money(usage.Amount));
    }

    protected override async Task ReloadAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var usage = Services.Collector.GitHub.Value;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthEnd = monthStart.AddMonths(1);
        var (from, to) = zoom ?? (monthStart, monthEnd);
        var history = await Task.Run(() => Services.Snapshots.GitHubHistory(from, to));

        var lines = new List<ChartLine>
        {
            new("github/spent", "Spent", null,
                history.Select(point => new HistoryPoint(point.Time, point.Amount, point.Amount, point.Amount)).ToList()),
        };
        if (usage?.IncludedAmount is { } included)
            lines.Add(new ChartLine("github/pace", "Even pace", null,
                [new HistoryPoint(monthStart, 0, 0, 0), new HistoryPoint(monthEnd, included, included, included)],
                IsReference: true));

        chart.Show(lines, value => Display.Money(value), from, to, 0, usage?.IncludedAmount);
        Refresh(now);
    }
}
