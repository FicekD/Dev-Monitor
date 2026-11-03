using Avalonia;
using Avalonia.Controls;
using DevMonitor.Core;

namespace DevMonitor.App.Controls;

public sealed class InstrumentRow : ContentControl
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<InstrumentRow, string?>(nameof(Label));
    public static readonly StyledProperty<string?> ValueTextProperty = AvaloniaProperty.Register<InstrumentRow, string?>(nameof(ValueText));
    public static readonly StyledProperty<string?> UnitProperty = AvaloniaProperty.Register<InstrumentRow, string?>(nameof(Unit));
    public static readonly StyledProperty<string?> DetailProperty = AvaloniaProperty.Register<InstrumentRow, string?>(nameof(Detail));
    public static readonly StyledProperty<Severity> SeverityProperty = AvaloniaProperty.Register<InstrumentRow, Severity>(nameof(Severity));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? ValueText { get => GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }
    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
    public Severity Severity { get => GetValue(SeverityProperty); set => SetValue(SeverityProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SeverityProperty)
        {
            PseudoClasses.Set(":warning", Severity == Severity.Warning);
            PseudoClasses.Set(":critical", Severity == Severity.Critical);
        }
    }
}
