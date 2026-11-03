using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DevMonitor.App.Controls;

public sealed class RangeBar : Control
{
    public static readonly StyledProperty<double> ScaleMinProperty = AvaloniaProperty.Register<RangeBar, double>(nameof(ScaleMin));
    public static readonly StyledProperty<double> ScaleMaxProperty = AvaloniaProperty.Register<RangeBar, double>(nameof(ScaleMax), 1);
    public static readonly StyledProperty<double> LowProperty = AvaloniaProperty.Register<RangeBar, double>(nameof(Low), double.NaN);
    public static readonly StyledProperty<double> HighProperty = AvaloniaProperty.Register<RangeBar, double>(nameof(High), double.NaN);
    public static readonly StyledProperty<double> CurrentProperty = AvaloniaProperty.Register<RangeBar, double>(nameof(Current), double.NaN);
    public static readonly StyledProperty<IBrush?> TrackBrushProperty = AvaloniaProperty.Register<RangeBar, IBrush?>(nameof(TrackBrush));
    public static readonly StyledProperty<IBrush?> RangeBrushProperty = AvaloniaProperty.Register<RangeBar, IBrush?>(nameof(RangeBrush));
    public static readonly StyledProperty<IBrush?> CurrentBrushProperty = AvaloniaProperty.Register<RangeBar, IBrush?>(nameof(CurrentBrush));

    static RangeBar() => AffectsRender<RangeBar>(ScaleMinProperty, ScaleMaxProperty, LowProperty, HighProperty,
        CurrentProperty, TrackBrushProperty, RangeBrushProperty, CurrentBrushProperty);

    public double ScaleMin { get => GetValue(ScaleMinProperty); set => SetValue(ScaleMinProperty, value); }
    public double ScaleMax { get => GetValue(ScaleMaxProperty); set => SetValue(ScaleMaxProperty, value); }
    public double Low { get => GetValue(LowProperty); set => SetValue(LowProperty, value); }
    public double High { get => GetValue(HighProperty); set => SetValue(HighProperty, value); }
    public double Current { get => GetValue(CurrentProperty); set => SetValue(CurrentProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? RangeBrush { get => GetValue(RangeBrushProperty); set => SetValue(RangeBrushProperty, value); }
    public IBrush? CurrentBrush { get => GetValue(CurrentBrushProperty); set => SetValue(CurrentBrushProperty, value); }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        if (width < 8)
            return;
        var middle = Bounds.Height / 2;
        context.DrawRectangle(TrackBrush, null, new Rect(0, middle - 1, width, 2), 1, 1);

        var span = ScaleMax - ScaleMin;
        if (span <= 0)
            return;
        double X(double value) => Math.Clamp((value - ScaleMin) / span, 0, 1) * width;

        if (!double.IsNaN(Low) && !double.IsNaN(High))
        {
            var left = X(Low);
            context.DrawRectangle(RangeBrush, null, new Rect(left, middle - 4, Math.Max(8, X(High) - left), 8), 4, 4);
        }
        if (!double.IsNaN(Current))
        {
            var x = Math.Round(X(Current));
            context.DrawRectangle(CurrentBrush, null, new Rect(Math.Clamp(x - 1, 0, width - 2), 0, 2, Bounds.Height), 1, 1);
        }
    }
}
