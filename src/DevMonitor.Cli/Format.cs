using DevMonitor.Core;
using DevMonitor.Core.Claude;
using DevMonitor.Core.GitHub;

namespace DevMonitor.Cli;

static class Format
{
    public static string Time(DateTimeOffset time) => time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public static string Duration(TimeSpan span) =>
        span.TotalDays >= 1 ? $"{(int)span.TotalDays}d {span.Hours}h"
        : span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m"
        : span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes}m {span.Seconds}s"
        : $"{Math.Max(0, (int)span.TotalSeconds)}s";

    public static string Age(DateTimeOffset time) => Duration(DateTimeOffset.UtcNow - time) + " ago";

    public static string Window(UsageWindow? window)
    {
        if (window is null)
            return "unavailable";
        var used = window.Utilization is { } utilization ? $"{utilization:0.#}%" : "usage unknown";
        var resets = window.ResetsAt is { } at ? $", resets {Time(at)} (in {Duration(at - DateTimeOffset.UtcNow)})" : "";
        return used + resets;
    }

    public static IEnumerable<string> Claude(ClaudeUsage usage)
    {
        yield return $"5-hour   {Window(usage.FiveHour)}";
        yield return $"weekly   {Window(usage.SevenDay)}";
        foreach (var (name, window) in usage.ModelWindows)
            yield return $"weekly {name}   {Window(window)}";
    }

    public static IEnumerable<string> GitHub(ActionsUsage usage)
    {
        var quota = usage.IncludedAmount is { } included
            ? $" of ${included:0.00} ({usage.Amount / included:P0}), ${Math.Max(0, included - usage.Amount):0.00} left"
            : " (included amount unknown)";
        yield return $"plan     {usage.Plan}";
        yield return $"used     ${usage.Amount:0.00}{quota}";
        yield return $"resets   {usage.ResetsOn:yyyy-MM-dd}";
        foreach (var (sku, skuUsage) in usage.Skus.OrderBy(pair => pair.Key))
            yield return $"         {sku}: {skuUsage.Minutes:N0} min, ${skuUsage.Amount:0.00}";
    }

    public static string State<T>(SourceState<T> state) where T : class
    {
        var now = DateTimeOffset.UtcNow;
        var updated = state.UpdatedAt is { } at ? $"updated {Age(at)}" : "never updated";
        var stale = state.IsStale(now) ? ", STALE" : "";
        var error = state.Error is { } message ? $", last attempt failed {Age(state.ErrorAt!.Value)}: {message}" : "";
        return updated + stale + error;
    }
}
