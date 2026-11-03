using Avalonia.Controls;

namespace DevMonitor.App.Controls;

public sealed record TimeRange(string Key, string Title, TimeSpan? Span)
{
    public static readonly TimeRange Day = new("24h", "24 hours", TimeSpan.FromDays(1));
    public static readonly TimeRange Week = new("7d", "7 days", TimeSpan.FromDays(7));
    public static readonly TimeRange Month = new("30d", "30 days", TimeSpan.FromDays(30));
    public static readonly TimeRange All = new("all", "All", null);

    public override string ToString() => Title;
}

public sealed class RangePicker : ListBox
{
    protected override Type StyleKeyOverride => typeof(ListBox);

    public RangePicker(IReadOnlyList<TimeRange> ranges, string? selectedKey)
    {
        Classes.Add("segmented");
        SelectionMode = SelectionMode.Single | SelectionMode.AlwaysSelected;
        ItemsSource = ranges;
        SelectedItem = ranges.FirstOrDefault(range => range.Key == selectedKey) ?? ranges[0];
    }

    public TimeRange Range => (TimeRange)SelectedItem!;
}
