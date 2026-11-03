using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DevMonitor.App.Controls;
using DevMonitor.App.Hardware;
using DevMonitor.App.Shell;
using DevMonitor.Core.Sensors;

namespace DevMonitor.App.Configuration;

public sealed class RecordedSensorsView : LiveView
{
    readonly SensorNotices notices = new() { Margin = new Thickness(0, 0, 0, 12) };
    readonly StackPanel groups = new() { Spacing = 2 };
    readonly Dictionary<string, TextBlock> values = [];
    readonly Dictionary<string, (TextBlock Count, List<SensorInfo> Sensors)> counts = [];
    string shownSensors = "";

    public RecordedSensorsView(AppServices services) : base(services)
    {
        var content = new StackPanel();
        content.Children.Add(notices);
        content.Children.Add(groups);
        Content = new Section { Title = "Recorded sensors", Content = content };
    }

    protected override void Refresh(DateTimeOffset now)
    {
        notices.Show(Services.Collector.SensorProblems);
        var readings = Services.Collector.Sensors.Value ?? [];
        var signature = string.Join('\n', readings.Select(reading => reading.Sensor.Key));
        if (signature != shownSensors)
        {
            shownSensors = signature;
            Build(readings);
        }
        foreach (var reading in readings)
            if (values.TryGetValue(reading.Sensor.Key, out var value))
                value.Text = Display.ReadingWithUnit(reading.Sensor, reading.Value);
    }

    void Build(IReadOnlyList<SensorReading> readings)
    {
        groups.Children.Clear();
        values.Clear();
        counts.Clear();
        foreach (var hardware in readings.GroupBy(reading => reading.Sensor.Hardware))
        {
            var sensors = hardware.Select(reading => reading.Sensor).OrderBy(sensor => sensor.Kind).ThenBy(sensor => sensor.Name).ToList();
            var count = new TextBlock { Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
            counts[hardware.Key] = (count, sensors);
            var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Grid.SetColumn(count, 1);
            header.Children.Add(new TextBlock { Text = hardware.Key, FontWeight = Avalonia.Media.FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center });
            header.Children.Add(count);

            var rows = new StackPanel();
            foreach (var sensor in sensors)
                rows.Children.Add(Row(sensor));
            groups.Children.Add(new Expander { Header = header, Content = rows, IsExpanded = false });
            UpdateCount(hardware.Key);
        }
    }

    void UpdateCount(string hardware)
    {
        var (count, sensors) = counts[hardware];
        count.Text = $"{sensors.Count(Services.Settings.IsRecorded)} of {sensors.Count} recorded";
    }

    Control Row(SensorInfo sensor)
    {
        var check = new CheckBox { Content = sensor.Name, IsChecked = Services.Settings.IsRecorded(sensor) };
        check.IsCheckedChanged += (_, _) =>
        {
            Services.UpdateSettings(settings => settings.WithRecorded(sensor, check.IsChecked == true));
            UpdateCount(sensor.Hardware);
        };
        var kind = new TextBlock { Text = sensor.Kind.ToString(), Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
        var value = new TextBlock { Classes = { "figure" }, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        values[sensor.Key] = value;

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,140,120"), MinHeight = 32 };
        Grid.SetColumn(kind, 1);
        Grid.SetColumn(value, 2);
        row.Children.AddRange([check, kind, value]);
        return row;
    }
}
