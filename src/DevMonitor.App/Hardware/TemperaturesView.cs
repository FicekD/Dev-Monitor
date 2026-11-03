using Avalonia;
using Avalonia.Controls;
using DevMonitor.App.Controls;
using DevMonitor.App.Shell;
using DevMonitor.Core.Sensors;
using DevMonitor.Core.Storage;

namespace DevMonitor.App.Hardware;

public sealed class TemperaturesView : LiveView
{
    readonly Section section;
    readonly SensorNotices notices = new() { Margin = new Thickness(0, 0, 0, 12) };
    readonly StackPanel groups = new() { Spacing = 16 };
    readonly Dictionary<string, (InstrumentRow Row, RangeBar Bar)> rows = [];
    readonly Dictionary<string, StackPanel> hardwareGroups = [];
    IReadOnlyDictionary<string, SensorSummary> summaries = new Dictionary<string, SensorSummary>();

    public TemperaturesView(AppServices services) : base(services)
    {
        var content = new StackPanel();
        content.Children.Add(notices);
        content.Children.Add(groups);
        section = new Section { Title = "Temperatures", Content = content };
        Content = section;
    }

    protected override async Task ReloadAsync()
    {
        var to = DateTimeOffset.UtcNow;
        summaries = await Task.Run(() =>
        {
            var byId = Services.Sensors.Summaries(to.AddDays(-1), to);
            return (IReadOnlyDictionary<string, SensorSummary>)Services.Sensors.Sensors()
                .Where(sensor => byId.ContainsKey(sensor.Id))
                .ToDictionary(sensor => sensor.Info.Key, sensor => byId[sensor.Id]);
        });
        Refresh(to);
    }

    protected override void Refresh(DateTimeOffset now)
    {
        var state = Services.Collector.Sensors;
        section.ShowState(state, now);
        notices.Show(Services.Collector.SensorProblems);

        var settings = Services.Settings;
        var temperatures = (state.Value ?? [])
            .Where(reading => reading.Sensor.Kind == SensorKind.Temperature && settings.IsRecorded(reading.Sensor))
            .OrderBy(reading => reading.Sensor.Hardware).ThenBy(reading => reading.Sensor.Name)
            .ToList();
        if (temperatures.Count == 0)
            return;

        var values = temperatures.Select(reading => reading.Value)
            .Concat(summaries.Values.SelectMany(summary => new[] { summary.Min, summary.Max }))
            .ToList();
        var scaleMin = Math.Floor((values.Min() - 5) / 10) * 10;
        var scaleMax = Math.Ceiling((values.Max() + 5) / 10) * 10;

        foreach (var reading in temperatures)
        {
            var sensor = reading.Sensor;
            if (!rows.TryGetValue(sensor.Key, out var view))
            {
                var bar = new RangeBar();
                view = (new InstrumentRow { Label = sensor.Name, Unit = sensor.Unit, Content = bar }, bar);
                rows[sensor.Key] = view;
                Group(sensor.Hardware).Children.Add(view.Row);
            }
            view.Row.ValueText = Display.Reading(sensor.Kind, reading.Value);
            view.Bar.ScaleMin = scaleMin;
            view.Bar.ScaleMax = scaleMax;
            view.Bar.Current = reading.Value;
            if (summaries.TryGetValue(sensor.Key, out var summary))
            {
                view.Bar.Low = summary.Min;
                view.Bar.High = summary.Max;
                view.Row.Detail = $"{summary.Min:0}–{summary.Max:0} in 24 h";
            }
        }
    }

    StackPanel Group(string hardware)
    {
        if (hardwareGroups.TryGetValue(hardware, out var group))
            return group;
        var container = new StackPanel();
        container.Children.Add(new TextBlock { Text = hardware, Classes = { "group" }, Margin = new Thickness(0, 0, 0, 2) });
        group = new StackPanel { Classes = { "rows" } };
        container.Children.Add(group);
        groups.Children.Add(container);
        hardwareGroups[hardware] = group;
        return group;
    }
}
