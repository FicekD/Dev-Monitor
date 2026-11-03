using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DevMonitor.Core;
using Serilog.Events;

namespace DevMonitor.App.Shell;

public sealed class LogConsole : UserControl
{
    const int Capacity = 1000;
    static readonly LogEventLevel[] Levels = [LogEventLevel.Debug, LogEventLevel.Information, LogEventLevel.Warning, LogEventLevel.Error];

    readonly UiState ui;
    readonly ObservableCollection<LogEntry> lines = [];
    readonly ListBox list;

    public LogConsole(UiState ui)
    {
        this.ui = ui;
        var level = new ComboBox
        {
            Width = 120,
            VerticalAlignment = VerticalAlignment.Top,
            ItemsSource = Levels.Select(Logging.LevelName).ToList(),
            SelectedIndex = Math.Max(0, Array.FindIndex(Levels, level => level.ToString() == ui.LogLevel)),
        };
        level.SelectionChanged += (_, _) =>
        {
            ui.LogLevel = Levels[level.SelectedIndex].ToString();
            Reload();
        };

        list = new ListBox
        {
            Classes = { "log" },
            ItemsSource = lines,
            ItemTemplate = new FuncDataTemplate<LogEntry?>((entry, _) => entry is null ? new Panel() : Line(entry)),
        };
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(40, 14, 40, 12) };
        Grid.SetColumn(level, 1);
        level.Margin = new Thickness(24, 0, 0, 0);
        layout.Children.AddRange([list, level]);
        Content = layout;
    }

    LogEventLevel Minimum => Enum.TryParse<LogEventLevel>(ui.LogLevel, out var level) ? level : LogEventLevel.Information;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Logging.Buffer.Added += OnAdded;
        Reload();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Logging.Buffer.Added -= OnAdded;
        base.OnDetachedFromVisualTree(e);
    }

    void Reload()
    {
        lines.Clear();
        foreach (var entry in Logging.Buffer.Snapshot().Where(entry => entry.Level >= Minimum))
            lines.Add(entry);
        ScrollToEnd();
    }

    void OnAdded(LogEntry entry) => Dispatcher.UIThread.Post(() =>
    {
        if (entry.Level < Minimum)
            return;
        var follow = list.Scroll is not { } scroll || scroll.Offset.Y + scroll.Viewport.Height >= scroll.Extent.Height - 8;
        lines.Add(entry);
        if (lines.Count > Capacity)
            lines.RemoveAt(0);
        if (follow)
            ScrollToEnd();
    });

    void ScrollToEnd()
    {
        if (lines.Count > 0)
            list.ScrollIntoView(lines.Count - 1);
    }

    static Control Line(LogEntry entry)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("72,72,150,*"), Classes = { "log-row" } };
        row.Classes.Add(entry.Level switch
        {
            >= LogEventLevel.Error => "error",
            LogEventLevel.Warning => "warning",
            LogEventLevel.Information => "info",
            _ => "debug",
        });
        var message = entry.Exception is { } exception ? entry.Message + Environment.NewLine + exception : entry.Message;
        Control[] cells =
        [
            new TextBlock { Text = entry.Time.ToLocalTime().ToString("HH:mm:ss"), Classes = { "log-time" } },
            new TextBlock { Text = Logging.LevelName(entry.Level), Classes = { "log-level" } },
            new TextBlock { Text = entry.Source, Classes = { "log-source" }, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 12, 0) },
            new SelectableTextBlock { Text = message, Classes = { "log-message" }, TextWrapping = TextWrapping.Wrap },
        ];
        for (var column = 0; column < cells.Length; column++)
        {
            Grid.SetColumn(cells[column], column);
            row.Children.Add(cells[column]);
        }
        return row;
    }
}
