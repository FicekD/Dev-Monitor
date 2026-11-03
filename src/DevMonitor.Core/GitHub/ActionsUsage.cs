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
}
