using Avalonia;
using Avalonia.Controls;
using DevMonitor.App.Controls;
using DevMonitor.App.Shell;
using DevMonitor.Core;
using DevMonitor.Core.GitHub;

namespace DevMonitor.App.GitHub;

public sealed class ActionsBudgetView : LiveView
{
    readonly Section section;
    readonly Meter meter = new();
    readonly InstrumentRow month;
    readonly StackPanel runners = new() { Classes = { "rows" } };
    readonly Dictionary<string, (InstrumentRow Row, Meter Meter)> runnerRows = [];
    readonly bool detailed;

    public ActionsBudgetView(AppServices services, string title, bool detailed) : base(services)
    {
        this.detailed = detailed;
        month = new InstrumentRow { Label = "This month", Content = meter };
        var rows = new StackPanel { Classes = { "rows" } };
        rows.Children.Add(month);
        if (detailed)
        {
            rows.Children.Add(new Border { Classes = { "rule" }, Margin = new Thickness(0, 8) });
            rows.Children.Add(runners);
        }
        section = new Section { Title = title, Content = rows };
        Content = section;
    }

    protected override void Refresh(DateTimeOffset now)
    {
        var state = Services.Collector.GitHub;
        section.ShowState(state, now);
        if (state.Value is not { } usage)
        {
            month.ValueText = "–";
            return;
        }

        var used = usage.IncludedAmount is { } included ? usage.Amount / included : (double?)null;
        month.ValueText = Display.Money(usage.Amount);
        month.Unit = usage.IncludedAmount is { } limit ? $"of {Display.Money(limit)}" : null;
        month.Detail = Display.ResetsIn(new DateTimeOffset(usage.ResetsOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), now);
        month.Severity = used is { } fraction ? Severities.ForUsage(fraction) : Severity.Normal;
        meter.Value = used ?? 0;
        meter.Pace = usage.ElapsedFraction(now);

        if (detailed)
            ShowRunners(usage);
    }

    void ShowRunners(ActionsUsage usage)
    {
        foreach (var (sku, runner) in usage.Skus.OrderByDescending(pair => pair.Value.Amount))
        {
            if (!runnerRows.TryGetValue(sku, out var view))
            {
                var runnerMeter = new Meter();
                view = (new InstrumentRow { Label = sku.Replace("Actions ", ""), Unit = "min", Content = runnerMeter }, runnerMeter);
                runnerRows[sku] = view;
                runners.Children.Add(view.Row);
            }
            view.Row.ValueText = runner.Minutes.ToString("0");
            view.Row.Detail = Display.Money(runner.Amount);
            view.Meter.Value = usage.IncludedAmount is { } included ? runner.Amount / included : 0;
        }
    }
}
