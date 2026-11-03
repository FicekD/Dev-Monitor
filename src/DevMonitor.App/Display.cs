using DevMonitor.Core.Sensors;

namespace DevMonitor.App;

static class Display
{
    public static string Ago(DateTimeOffset time, DateTimeOffset now)
    {
        var age = now - time;
        return age.TotalMinutes < 1 ? "just now"
            : age.TotalHours < 1 ? $"{(int)age.TotalMinutes} min ago"
            : age.TotalDays < 1 ? $"{(int)age.TotalHours} h ago"
            : $"{(int)age.TotalDays} d ago";
    }

    public static string Remaining(TimeSpan span) =>
        span.TotalDays >= 1 ? $"{(int)span.TotalDays}d {span.Hours}h"
        : span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m"
        : $"{Math.Max(0, (int)Math.Ceiling(span.TotalMinutes))}m";

    public static string ResetsIn(DateTimeOffset? at, DateTimeOffset now) =>
        at is { } time ? $"resets in {Remaining(time - now)}" : "";

    public static string Percent(double? value) => value is { } v ? $"{v:0}%" : "–";

    public static string Money(double value) => $"${value:0.00}";

    public static string Time(DateTimeOffset time) => time.ToLocalTime().ToString("g");

    public static string Reading(SensorKind kind, double value) => kind switch
    {
        SensorKind.Voltage => value.ToString("0.000"),
        SensorKind.Power => value.ToString("0.0"),
        _ => value.ToString("0"),
    };

    public static string ReadingWithUnit(SensorInfo sensor, double value) =>
        sensor.Unit.Length == 0 ? Reading(sensor.Kind, value) : $"{Reading(sensor.Kind, value)} {sensor.Unit}";
}
