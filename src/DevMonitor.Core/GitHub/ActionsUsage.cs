namespace DevMonitor.Core.GitHub;

public sealed record SkuUsage(double Minutes, double Amount);

public sealed record ActionsUsage(
    DateTimeOffset FetchedAt,
    string Plan,
    double? IncludedAmount,
    IReadOnlyDictionary<string, SkuUsage> Skus,
    DateOnly ResetsOn)
{
    public double Amount => Skus.Values.Sum(sku => sku.Amount);

    public double Minutes => Skus.Values.Sum(sku => sku.Minutes);

    public double ElapsedFraction(DateTimeOffset now)
    {
        var end = new DateTimeOffset(ResetsOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var start = end.AddMonths(-1);
        return Math.Clamp((now - start) / (end - start), 0, 1);
    }
}
