using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using DevMonitor.App.Controls;
using DevMonitor.App.Shell;
using DevMonitor.Core.Sensors;
using DevMonitor.Core.Storage;

namespace DevMonitor.App.Hardware;

public sealed class HardwarePage : UserControl
{
    const string StateKey = "hardware";

    public HardwarePage(AppServices services)
    {
        services.Ui.Ranges.TryGetValue(StateKey, out var selected);
        var picker = new RangePicker([TimeRange.Day, TimeRange.Week, TimeRange.Month, TimeRange.All], selected);
        picker.SelectionChanged += (_, _) => services.Ui.Ranges[StateKey] = picker.Range.Key;
        Content = Pages.Layout("Hardware", picker, wide: true, new HardwareChartsView(services, picker));
    }
}

public sealed class HardwareChartsView : LiveView
{
    static readonly (SensorKind Kind, string Title, Func<double, string> Format, double? Min, double? Max)[] Kinds =
    [
        (SensorKind.Temperature, "Temperatures", value => $"{value:0} °C", null, null),
        (SensorKind.Fan, "Fan speed", value => $"{value:0} RPM", 0, null),
        (SensorKind.Control, "Fan duty", value => $"{value:0}%", 0, 100),
        (SensorKind.Load, "Load", value => $"{value:0}%", 0, 100),
        (SensorKind.Power, "Power", value => $"{value:0.#} W", 0, null),
        (SensorKind.Clock, "Clock speed", value => $"{value:0} MHz", 0, null),
        (SensorKind.Voltage, "Voltage", value => $"{value:0.###} V", null, null),
        (SensorKind.Other, "Other", value => $"{value:0.##}", null, null),
    ];

    readonly RangePicker picker;
    readonly SensorNotices notices = new();
    readonly Section status = new() { Title = "Sensors" };
    const double MinimumChartWidth = 640;
    readonly UniformGrid charts = new() { Columns = 1, Margin = new Thickness(0, 0, -32, 0) };
    readonly Dictionary<SensorKind, (Section Section, HistoryChart Chart)> sections = [];
    (DateTimeOffset From, DateTimeOffset To)? zoom;

    public HardwareChartsView(AppServices services, RangePicker picker) : base(services)
    {
        this.picker = picker;
        picker.SelectionChanged += (_, _) =>
        {
            zoom = null;
            Reload();
        };
        status.Content = notices;
        var content = new StackPanel { Spacing = 36 };
        content.Children.Add(status);
        content.Children.Add(charts);
        Content = content;
        content.PropertyChanged += (_, change) =>
        {
            if (change.Property == BoundsProperty)
                charts.Columns = Math.Max(1, (int)((content.Bounds.Width + 32) / (MinimumChartWidth + 32)));
        };
    }

    protected override void Refresh(DateTimeOffset now)
    {
        status.ShowState(Services.Collector.Sensors, now);
        notices.Show(Services.Collector.SensorProblems);
        foreach (var reading in Services.Collector.Sensors.Value ?? [])
            if (sections.TryGetValue(reading.Sensor.Kind, out var view))
                view.Chart.SetValue(reading.Sensor.Key, Display.Reading(reading.Sensor.Kind, reading.Value));
    }

    protected override async Task ReloadAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var range = picker.Range;
        var settings = Services.Settings;
        var zoomed = zoom;
        var (from, to, histories) = await Task.Run(() =>
        {
            var sensors = Services.Sensors.Sensors().Where(sensor => settings.IsRecorded(sensor.Info)).OrderBy(sensor => sensor.Id).ToList();
            var (start, end) = zoomed ?? (range.Span is { } span
                ? now - span
                : sensors.Select(sensor => Services.Sensors.FirstReading(sensor.Id)).Where(first => first is not null).Min() ?? now.AddHours(-1), now);
            var lines = sensors.ToDictionary(sensor => sensor, sensor => Services.Sensors.History(sensor.Id, start, end, 600));
            return (start, end, lines);
        });

        foreach (var (kind, title, format, min, max) in Kinds)
        {
            var lines = histories
                .Where(pair => pair.Key.Info.Kind == kind)
                .Select(pair => new ChartLine(pair.Key.Info.Key, pair.Key.Info.Name, pair.Key.Info.Hardware, pair.Value))
                .ToList();
            if (lines.Count == 0)
            {
                if (sections.Remove(kind, out var stale))
                    charts.Children.Remove(stale.Section);
                continue;
            }
            if (!sections.TryGetValue(kind, out var view))
            {
                var chart = new HistoryChart(Services.Ui.HiddenSeries);
                chart.ZoomRequested += (start, end) =>
                {
                    zoom = (start, end);
                    Reload();
                };
                chart.ZoomReset += () =>
                {
                    zoom = null;
                    Reload();
                };
                view = (new Section { Title = title, Content = chart, Margin = new Thickness(0, 0, 32, 36) }, chart);
                sections[kind] = view;
                charts.Children.Insert(sections.Keys.Count(other => other < kind), view.Section);
            }
            view.Chart.Show(lines, format, from, to, min, max);
        }
        Refresh(now);
    }
}
