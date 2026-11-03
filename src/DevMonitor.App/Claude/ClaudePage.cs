using Avalonia.Controls;
using DevMonitor.App.Controls;
using DevMonitor.App.Shell;
using DevMonitor.Core.Storage;

namespace DevMonitor.App.Claude;

public sealed class ClaudePage : UserControl
{
    public ClaudePage(AppServices services) =>
        Content = Pages.Layout("Claude", null, new ClaudeLimitsView(services, "Limits"), new ClaudeHistoryView(services));
}

public sealed class ClaudeHistoryView : LiveView
{
    const string StateKey = "claude";
    readonly RangePicker picker;
    readonly HistoryChart chart;
    (DateTimeOffset From, DateTimeOffset To)? zoom;

    public ClaudeHistoryView(AppServices services) : base(services)
    {
        services.Ui.Ranges.TryGetValue(StateKey, out var selected);
        picker = new RangePicker([TimeRange.Day, TimeRange.Week, TimeRange.Month], selected ?? TimeRange.Week.Key);
        picker.SelectionChanged += (_, _) =>
        {
            services.Ui.Ranges[StateKey] = picker.Range.Key;
            zoom = null;
            Reload();
        };
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
        Content = new Section { Title = "History", Actions = picker, Content = chart };
    }

    protected override TimeSpan ReloadInterval => TimeSpan.FromMinutes(5);

    protected override void Refresh(DateTimeOffset now)
    {
        var usage = Services.Collector.Claude.Value;
        chart.SetValue("claude/five_hour", Display.Percent(usage?.FiveHour?.Utilization));
        chart.SetValue("claude/seven_day", Display.Percent(usage?.SevenDay?.Utilization));
    }

    protected override async Task ReloadAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var (from, to) = zoom ?? (now - picker.Range.Span!.Value, now);
        var history = await Task.Run(() => Services.Snapshots.ClaudeHistory(from, to, 600));
        IReadOnlyList<HistoryPoint> Points(Func<ClaudePoint, double?> select) => history
            .Where(point => select(point) is not null)
            .Select(point => new HistoryPoint(point.Time, select(point)!.Value, select(point)!.Value, select(point)!.Value))
            .ToList();

        chart.Show(
            [
                new ChartLine("claude/five_hour", "5-hour", null, Points(point => point.FiveHour)),
                new ChartLine("claude/seven_day", "Weekly", null, Points(point => point.SevenDay)),
            ],
            value => $"{value:0}%", from, to, 0, 100);
        Refresh(now);
    }
}
