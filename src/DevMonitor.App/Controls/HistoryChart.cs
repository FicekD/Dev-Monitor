using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using DevMonitor.Core.Storage;
using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;

namespace DevMonitor.App.Controls;

public sealed record ChartLine(string Key, string Title, string? Subtitle, IReadOnlyList<HistoryPoint> Points, bool IsReference = false);

public sealed class HistoryChart : UserControl
{
    const int SeriesSlots = 8;
    const double ChartHeight = 260;
    const double SideLegendWidth = 260;
    const double SideLegendMinChartWidth = 480;
    static readonly TimeSpan MinimumZoom = TimeSpan.FromMinutes(2);
    static readonly TimeSpan[] Steps =
    [
        TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3), TimeSpan.FromHours(6), TimeSpan.FromHours(12),
        TimeSpan.FromDays(1), TimeSpan.FromDays(2), TimeSpan.FromDays(7), TimeSpan.FromDays(14), TimeSpan.FromDays(30),
        TimeSpan.FromDays(91), TimeSpan.FromDays(182), TimeSpan.FromDays(365),
    ];
    static readonly string DayMonthPattern = Regex.Replace(
        CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern, @"[^dM]*y+[^dM]*", "");

    readonly CartesianChart chart = new()
    {
        Height = ChartHeight,
        LegendPosition = LegendPosition.Hidden,
        TooltipPosition = TooltipPosition.Hidden,
        ZoomMode = ZoomAndPanMode.None,
        AnimationsSpeed = TimeSpan.FromMilliseconds(200),
        Cursor = new Cursor(StandardCursorType.Cross),
    };
    readonly Border selection = new() { IsVisible = false, CornerRadius = new CornerRadius(4), Opacity = 0.55 };
    readonly Grid layout = new();
    readonly ScrollViewer legendScroller;
    readonly WrapPanel legend = new();
    readonly Dictionary<string, LegendEntry> entries = [];
    readonly ISet<string> hidden;
    IReadOnlyList<ChartLine> lines = [];
    Func<double, string> format = value => value.ToString("0");
    DateTimeOffset from, to;
    double? minimum, maximum;
    double? dragStart;

    public event Action<DateTimeOffset, DateTimeOffset>? ZoomRequested;
    public event Action? ZoomReset;

    public HistoryChart(ISet<string> hidden)
    {
        this.hidden = hidden;
        selection.Bind(Border.BackgroundProperty, selection.GetResourceObservable("AccentSoftBrush"));
        var overlay = new Canvas { IsHitTestVisible = false, Children = { selection } };
        var plot = new Panel { Children = { chart, overlay } };

        legendScroller = new ScrollViewer { Content = legend };
        layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        layout.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0)));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.Children.Add(plot);
        layout.Children.Add(legendScroller);
        Content = layout;

        chart.AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        chart.AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        chart.AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        ActualThemeVariantChanged += (_, _) => Render();
        PropertyChanged += (_, change) =>
        {
            if (change.Property == BoundsProperty)
                ArrangeLegend();
        };
    }

    public void Show(IReadOnlyList<ChartLine> lines, Func<double, string> format, DateTimeOffset from, DateTimeOffset to,
        double? minimum = null, double? maximum = null)
    {
        this.lines = lines;
        this.format = format;
        this.from = from;
        this.to = to;
        this.minimum = minimum;
        this.maximum = maximum;
        BuildLegend();
        ArrangeLegend();
        Render();
    }

    public void SetValue(string key, string? text)
    {
        if (entries.TryGetValue(key, out var entry))
            entry.ValueText = text;
    }

    void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(chart);
        if (!point.Properties.IsLeftButtonPressed)
            return;
        dragStart = point.Position.X;
        e.Pointer.Capture(chart);
    }

    void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (dragStart is not { } start)
            return;
        var x = Math.Clamp(e.GetPosition(chart).X, 0, chart.Bounds.Width);
        Canvas.SetLeft(selection, Math.Min(start, x));
        selection.Width = Math.Abs(x - start);
        selection.Height = chart.Bounds.Height;
        selection.IsVisible = selection.Width > 2;
    }

    void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            ZoomReset?.Invoke();
            return;
        }
        if (dragStart is not { } start)
            return;
        dragStart = null;
        selection.IsVisible = false;
        e.Pointer.Capture(null);

        var end = Math.Clamp(e.GetPosition(chart).X, 0, chart.Bounds.Width);
        if (Math.Abs(end - start) < 8)
            return;
        var left = TimeAt(Math.Min(start, end));
        var right = TimeAt(Math.Max(start, end));
        if (right - left < MinimumZoom)
            right = left + MinimumZoom;
        ZoomRequested?.Invoke(left, right);
    }

    DateTimeOffset TimeAt(double x)
    {
        var ticks = chart.ScalePixelsToData(new LvcPointD(x, 0)).X;
        var time = new DateTimeOffset(new DateTime((long)Math.Clamp(ticks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks), DateTimeKind.Local));
        return time < from ? from : time > to ? to : time;
    }

    void BuildLegend()
    {
        var keys = lines.Select(line => line.Key).ToHashSet();
        foreach (var key in entries.Keys.Where(key => !keys.Contains(key)).ToList())
        {
            legend.Children.Remove(entries[key]);
            entries.Remove(key);
        }
        foreach (var line in lines)
        {
            if (entries.ContainsKey(line.Key))
                continue;
            var entry = new LegendEntry { Title = line.Title, Subtitle = line.Subtitle, IsChecked = !hidden.Contains(line.Key), Width = 240 };
            var key = line.Key;
            entry.IsCheckedChanged += (_, _) =>
            {
                if (entry.IsChecked == true)
                    hidden.Remove(key);
                else
                    hidden.Add(key);
                Render();
            };
            entries[key] = entry;
            legend.Children.Add(entry);
        }
        legendScroller.IsVisible = lines.Count > 1;
    }

    void ArrangeLegend()
    {
        var side = Bounds.Width - SideLegendWidth >= SideLegendMinChartWidth;
        layout.ColumnDefinitions[1].Width = new GridLength(side && legendScroller.IsVisible ? SideLegendWidth : 0);
        Grid.SetColumn(legendScroller, side ? 1 : 0);
        Grid.SetRow(legendScroller, side ? 0 : 1);
        legendScroller.MaxHeight = side ? ChartHeight : 150;
        legendScroller.Margin = side ? new Thickness(20, 0, 0, 0) : new Thickness(0, 12, 0, 0);
    }

    void Render()
    {
        var muted = ThemeColor("InkMutedColor");
        var faint = ThemeColor("InkFaintColor");
        var rule = ThemeColor("RuleColor");
        var labelBackground = ThemeColor("SunkenColor");

        var series = new List<ISeries>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var color = line.IsReference || index >= SeriesSlots ? faint : ThemeColor($"Series{index + 1}Color");
            if (entries.TryGetValue(line.Key, out var entry))
                entry.Swatch = new SolidColorBrush(color);
            var stroke = new SolidColorPaint(Sk(color), line.IsReference ? 1.5f : 2f);
            if (line.IsReference)
                stroke.PathEffect = new DashEffect([5, 5]);
            series.Add(new LineSeries<HistoryPoint>
            {
                Values = WithGaps(line.Points),
                Mapping = (point, _) => double.IsNaN(point.Avg)
                    ? Coordinate.Empty
                    : new Coordinate(point.Time.LocalDateTime.Ticks, point.Avg),
                Name = line.Title,
                Stroke = stroke,
                Fill = null,
                GeometrySize = 0,
                GeometryFill = null,
                GeometryStroke = null,
                LineSmoothness = 0,
                IsVisible = !hidden.Contains(line.Key),
            });
        }

        var start = from.LocalDateTime;
        var end = to.LocalDateTime;
        var step = Steps.FirstOrDefault(candidate => (end - start) / candidate <= 7, Steps[^1]);
        var labelFormat = step < TimeSpan.FromDays(1) ? "HH:mm" : DayMonthPattern;

        chart.XAxes =
        [
            new Axis
            {
                MinLimit = start.Ticks,
                MaxLimit = end.Ticks,
                CustomSeparators = Separators(start, end, step).ToArray(),
                Labeler = value => new DateTime((long)value).ToString(labelFormat),
                LabelsPaint = new SolidColorPaint(Sk(muted)),
                TextSize = 12,
                SeparatorsPaint = null,
                CrosshairPaint = new SolidColorPaint(Sk(faint), 1),
                CrosshairLabelsPaint = new SolidColorPaint(Sk(ThemeColor("InkColor"))),
                CrosshairLabelsBackground = Lvc(labelBackground),
            },
        ];
        chart.YAxes =
        [
            new Axis
            {
                MinLimit = minimum,
                MaxLimit = maximum,
                Labeler = value => format(value),
                LabelsPaint = new SolidColorPaint(Sk(muted)),
                TextSize = 12,
                SeparatorsPaint = new SolidColorPaint(Sk(rule), 1),
                CrosshairPaint = new SolidColorPaint(Sk(faint), 1),
                CrosshairLabelsPaint = new SolidColorPaint(Sk(ThemeColor("InkColor"))),
                CrosshairLabelsBackground = Lvc(labelBackground),
            },
        ];
        chart.Series = series;
    }

    static IEnumerable<double> Separators(DateTime start, DateTime end, TimeSpan step)
    {
        var time = step < TimeSpan.FromDays(1) ? new DateTime(start.Ticks - start.Ticks % step.Ticks) : start.Date;
        for (; time <= end; time += step)
            if (time >= start)
                yield return time.Ticks;
    }

    static List<HistoryPoint> WithGaps(IReadOnlyList<HistoryPoint> points)
    {
        var result = new List<HistoryPoint>(points.Count + 8);
        if (points.Count < 3)
        {
            result.AddRange(points);
            return result;
        }
        var spacings = points.Zip(points.Skip(1), (a, b) => b.Time - a.Time).Order().ToList();
        var spacing = spacings[spacings.Count / 2];
        for (var index = 0; index < points.Count; index++)
        {
            if (index > 0 && points[index].Time - points[index - 1].Time > spacing * 4)
                result.Add(new HistoryPoint(points[index - 1].Time, double.NaN, double.NaN, double.NaN));
            result.Add(points[index]);
        }
        return result;
    }

    Color ThemeColor(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is Color color ? color : Colors.Gray;

    static SKColor Sk(Color color) => new(color.R, color.G, color.B, color.A);

    static LvcColor Lvc(Color color) => new(color.R, color.G, color.B, color.A);
}
