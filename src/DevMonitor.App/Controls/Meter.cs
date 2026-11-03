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
        const double barHeight = 10;
        var width = Bounds.Width;
        if (width < barHeight)
            return;
        var bar = new Rect(0, (Bounds.Height - barHeight) / 2, width, barHeight);
        context.DrawRectangle(TrackBrush, null, bar, 4, 4);

        var value = Math.Clamp(Value, 0, 1);
        if (value > 0)
        {
            var fill = Severities.ForUsage(value) switch
            {
                Severity.Critical => CriticalBrush,
                Severity.Warning => WarningBrush,
                _ => FillBrush,
            };
            context.DrawRectangle(fill, null, bar.WithWidth(Math.Max(barHeight, width * value)), 4, 4);
        }

        if (!double.IsNaN(Pace))
        {
            var x = Math.Round(width * Math.Clamp(Pace, 0, 1));
            context.DrawRectangle(PaceBrush, null, new Rect(Math.Clamp(x - 1, 0, width - 2), 0, 2, Bounds.Height), 1, 1);
        }
    }
}
