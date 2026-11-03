using System.Globalization;
using System.Text.Json;

namespace DevMonitor.Core.Claude;

public sealed record UsageWindow(double? Utilization, DateTimeOffset? ResetsAt);

public sealed record ClaudeUsage(
    DateTimeOffset FetchedAt,
    UsageWindow? FiveHour,
    UsageWindow? SevenDay,
    IReadOnlyDictionary<string, UsageWindow> ModelWindows)
{
    const string ModelWindowPrefix = "seven_day_";

    public static ClaudeUsage Parse(string json, DateTimeOffset fetchedAt)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var modelWindows = new Dictionary<string, UsageWindow>();
        if (root.ValueKind != JsonValueKind.Object)
            return new(fetchedAt, null, null, modelWindows);

        foreach (var property in root.EnumerateObject())
            if (property.Name.StartsWith(ModelWindowPrefix) && ReadWindow(property.Value) is { } window)
                modelWindows[property.Name[ModelWindowPrefix.Length..]] = window;

        return new(
            fetchedAt,
            root.TryGetProperty("five_hour", out var fiveHour) ? ReadWindow(fiveHour) : null,
            root.TryGetProperty("seven_day", out var sevenDay) ? ReadWindow(sevenDay) : null,
            modelWindows);
    }

    static UsageWindow? ReadWindow(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        double? utilization = element.TryGetProperty("utilization", out var u) && u.ValueKind == JsonValueKind.Number
            ? u.GetDouble()
            : null;
        DateTimeOffset? resetsAt = element.TryGetProperty("resets_at", out var r)
            && r.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(r.GetString(), CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        return utilization is null && resetsAt is null ? null : new(utilization, resetsAt);
    }
}
