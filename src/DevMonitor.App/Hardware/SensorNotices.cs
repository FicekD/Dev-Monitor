using Avalonia.Controls;
using DevMonitor.App.Controls;

namespace DevMonitor.App.Hardware;

public sealed class SensorNotices : StackPanel
{
    IReadOnlyList<string> shown = [];

    public SensorNotices() => Spacing = 8;

    public void Show(IReadOnlyList<string> problems)
    {
        if (problems.SequenceEqual(shown))
            return;
        shown = problems.ToList();
        Children.Clear();
        foreach (var problem in problems)
            Children.Add(new Notice { Text = problem });
        IsVisible = problems.Count > 0;
    }
}
