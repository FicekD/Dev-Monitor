namespace DevMonitor.Core;

public static class Durations
{
    public static string Short(TimeSpan span) =>
        span.TotalDays >= 1 ? $"{(int)span.TotalDays}d {span.Hours}h"
        : span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m"
        : span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes}m {span.Seconds}s"
        : $"{Math.Max(0, (int)span.TotalSeconds)}s";
}
