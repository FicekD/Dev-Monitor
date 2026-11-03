using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DevMonitor.Core;

namespace DevMonitor.App.Controls;

public sealed class Meter : Control
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<Meter, double>(nameof(Value));
    public static readonly StyledProperty<double> PaceProperty = AvaloniaProperty.Register<Meter, double>(nameof(Pace), double.NaN);
    public static readonly StyledProperty<IBrush?> TrackBrushProperty = AvaloniaProperty.Register<Meter, IBrush?>(nameof(TrackBrush));
    public static readonly StyledProperty<IBrush?> FillBrushProperty = AvaloniaProperty.Register<Meter, IBrush?>(nameof(FillBrush));
    public static readonly StyledProperty<IBrush?> WarningBrushProperty = AvaloniaProperty.Register<Meter, IBrush?>(nameof(WarningBrush));
    public static readonly StyledProperty<IBrush?> CriticalBrushProperty = AvaloniaProperty.Register<Meter, IBrush?>(nameof(CriticalBrush));
    public static readonly StyledProperty<IBrush?> PaceBrushProperty = AvaloniaProperty.Register<Meter, IBrush?>(nameof(PaceBrush));

    static Meter() => AffectsRender<Meter>(ValueProperty, PaceProperty, TrackBrushProperty, FillBrushProperty,
        WarningBrushProperty, CriticalBrushProperty, PaceBrushProperty);

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Pace { get => GetValue(PaceProperty); set => SetValue(PaceProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public IBrush? WarningBrush { get => GetValue(WarningBrushProperty); set => SetValue(WarningBrushProperty, value); }
    public IBrush? CriticalBrush { get => GetValue(CriticalBrushProperty); set => SetValue(CriticalBrushProperty, value); }
    public IBrush? PaceBrush { get => GetValue(PaceBrushProperty); set => SetValue(PaceBrushProperty, value); }

    public override void Render(DrawingContext context)
    {
        const double barHeight = 14;
        const double notch = 5;
        var width = Bounds.Width;
        if (width < barHeight)
            return;
        var bar = new Rect(0, (Bounds.Height - barHeight) / 2, width, barHeight);
        context.DrawRectangle(TrackBrush, null, bar, 5, 5);

        var value = Math.Clamp(Value, 0, 1);
        if (value > 0)
        {
            var fill = Severities.ForUsage(value) switch
            {
                Severity.Critical => CriticalBrush,
                Severity.Warning => WarningBrush,
                _ => FillBrush,
            };
            context.DrawRectangle(fill, null, bar.WithWidth(Math.Max(barHeight, width * value)), 5, 5);
        }

        if (!double.IsNaN(Pace))
        {
            var x = Math.Clamp(Math.Round(width * Math.Clamp(Pace, 0, 1)), notch, width - notch);
            context.DrawRectangle(PaceBrush, null, new Rect(x - 1, bar.Top - 2, 2, barHeight + 4));
            context.DrawGeometry(PaceBrush, null, Triangle(x, bar.Top - 2, -notch));
            context.DrawGeometry(PaceBrush, null, Triangle(x, bar.Bottom + 2, notch));
        }
    }

    static StreamGeometry Triangle(double x, double tipY, double height)
    {
        var geometry = new StreamGeometry();
        using var draw = geometry.Open();
        draw.BeginFigure(new Point(x, tipY), true);
        draw.LineTo(new Point(x - height * 0.9, tipY + height));
        draw.LineTo(new Point(x + height * 0.9, tipY + height));
        draw.EndFigure(true);
        return geometry;
    }
}
