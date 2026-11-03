using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace DevMonitor.App.Controls;

public sealed class FieldRow : ContentControl
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<FieldRow, string?>(nameof(Label));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
}

public sealed class Notice : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<Notice, string?>(nameof(Text));
    public static readonly StyledProperty<bool> IsCriticalProperty = AvaloniaProperty.Register<Notice, bool>(nameof(IsCritical));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsCritical { get => GetValue(IsCriticalProperty); set => SetValue(IsCriticalProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCriticalProperty)
            PseudoClasses.Set(":critical", IsCritical);
    }
}

public class RailButton : ToggleButton
{
    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<RailButton, Geometry?>(nameof(Icon));

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    protected override Type StyleKeyOverride => typeof(RailButton);
}

public sealed class NavButton : RailButton
{
    protected override void OnClick()
    {
        if (IsChecked != true)
            base.OnClick();
    }
}

public sealed class CaptionButton : Button
{
    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<CaptionButton, Geometry?>(nameof(Icon));

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
}

public sealed class LegendEntry : ToggleButton
{
    public static readonly StyledProperty<IBrush?> SwatchProperty = AvaloniaProperty.Register<LegendEntry, IBrush?>(nameof(Swatch));
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<LegendEntry, string?>(nameof(Title));
    public static readonly StyledProperty<string?> SubtitleProperty = AvaloniaProperty.Register<LegendEntry, string?>(nameof(Subtitle));
    public static readonly StyledProperty<string?> ValueTextProperty = AvaloniaProperty.Register<LegendEntry, string?>(nameof(ValueText));

    public IBrush? Swatch { get => GetValue(SwatchProperty); set => SetValue(SwatchProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string? ValueText { get => GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }
}
