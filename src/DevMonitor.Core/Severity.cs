namespace DevMonitor.Core;

public enum Severity { Normal, Warning, Critical }

public static class Severities
{
    public const double UsageWarning = 0.75;
    public const double UsageCritical = 0.9;

    public static Severity ForUsage(double fraction) =>
        fraction >= UsageCritical ? Severity.Critical : fraction >= UsageWarning ? Severity.Warning : Severity.Normal;
}
