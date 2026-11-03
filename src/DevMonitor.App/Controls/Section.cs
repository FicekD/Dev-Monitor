using Avalonia;
using Avalonia.Controls;
using DevMonitor.Core;

namespace DevMonitor.App.Controls;

public sealed class Section : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<Section, string?>(nameof(Title));
    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<Section, object?>(nameof(Actions));
    public static readonly StyledProperty<string?> StatusProperty = AvaloniaProperty.Register<Section, string?>(nameof(Status));
    public static readonly StyledProperty<bool> IsStaleProperty = AvaloniaProperty.Register<Section, bool>(nameof(IsStale));
    public static readonly StyledProperty<string?> ErrorProperty = AvaloniaProperty.Register<Section, string?>(nameof(Error));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
    public string? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public bool IsStale { get => GetValue(IsStaleProperty); set => SetValue(IsStaleProperty, value); }
    public string? Error { get => GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }

    public void ShowState<T>(SourceState<T> state, DateTimeOffset now) where T : class
    {
        IsStale = state.IsStale(now);
        Status = state.UpdatedAt is not { } updated ? "Waiting for the first update"
            : IsStale ? $"Out of date, last updated {Display.Ago(updated, now)}"
            : $"Updated {Display.Ago(updated, now)}";
        Error = state.Error is { } error ? $"The last refresh {Display.Ago(state.ErrorAt!.Value, now)} failed. {error}" : null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
            PseudoClasses.Set(":status", !string.IsNullOrEmpty(Status));
        else if (change.Property == IsStaleProperty)
            PseudoClasses.Set(":stale", IsStale);
        else if (change.Property == ErrorProperty)
            PseudoClasses.Set(":error", !string.IsNullOrEmpty(Error));
    }
}
