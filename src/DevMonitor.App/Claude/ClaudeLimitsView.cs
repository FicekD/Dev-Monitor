using Avalonia.Controls;
using DevMonitor.App.Controls;
using DevMonitor.App.Shell;
using DevMonitor.Core;
using DevMonitor.Core.Claude;

namespace DevMonitor.App.Claude;

public sealed class ClaudeLimitsView : LiveView
{
    readonly Section section;
    readonly StackPanel rows = new() { Classes = { "rows" } };
    readonly Dictionary<string, (InstrumentRow Row, Meter Meter)> windows = [];

    public ClaudeLimitsView(AppServices services, string title) : base(services)
    {
        section = new Section { Title = title, Content = rows };
        Content = section;
    }

    protected override void Refresh(DateTimeOffset now)
    {
        var state = Services.Collector.Claude;
        section.ShowState(state, now);
        var usage = state.Value;

        var shown = new List<(string Key, string Label, UsageWindow? Window, TimeSpan Length)>
        {
            ("five_hour", "5-hour", usage?.FiveHour, ClaudeUsage.FiveHourLength),
            ("seven_day", "Weekly", usage?.SevenDay, ClaudeUsage.WeekLength),
        };
        foreach (var (name, window) in usage?.ModelWindows ?? new Dictionary<string, UsageWindow>())
            shown.Add(("seven_day_" + name, ModelWindowLabel(name), window, ClaudeUsage.WeekLength));

        foreach (var key in windows.Keys.Except(shown.Select(window => window.Key)).ToList())
        {
            rows.Children.Remove(windows[key].Row);
            windows.Remove(key);
        }
        foreach (var (key, label, window, length) in shown)
        {
            if (!windows.TryGetValue(key, out var view))
            {
                var meter = new Meter();
                view = (new InstrumentRow { Label = label, Content = meter }, meter);
                windows[key] = view;
                rows.Children.Add(view.Row);
            }
            Show(view.Row, view.Meter, window, length, now);
        }
    }

    static void Show(InstrumentRow row, Meter meter, UsageWindow? window, TimeSpan length, DateTimeOffset now)
    {
        var used = window?.Utilization / 100;
        var elapsed = window?.ElapsedFraction(length, now);
        row.ValueText = Display.Percent(window?.Utilization);
        row.Detail = Display.ResetsIn(window?.ResetsAt, now);
        row.Severity = used is { } fraction ? Severities.ForUsage(fraction) : Severity.Normal;
        meter.Value = used ?? 0;
        meter.Pace = elapsed ?? double.NaN;
    }

    static string ModelWindowLabel(string name) => name switch
    {
        "opus" => "Weekly Opus",
        "sonnet" => "Weekly Sonnet",
        _ => "Weekly " + name.Replace('_', ' '),
    };
}
