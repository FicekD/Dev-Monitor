using System.Text.Json;
using DevMonitor.Core.Claude;
using DevMonitor.Core.GitHub;
using Microsoft.Data.Sqlite;

namespace DevMonitor.Core.Storage;

public sealed class SnapshotStore(Database database)
{
    public void Save(ClaudeUsage usage)
    {
        using var connection = database.Open();
        Database.Execute(connection, """
            INSERT OR REPLACE INTO claude_snapshots (ts, five_hour, five_hour_resets, seven_day, seven_day_resets, model_windows)
            VALUES ($ts, $fiveHour, $fiveHourResets, $sevenDay, $sevenDayResets, $modelWindows)
            """,
            ("$ts", usage.FetchedAt.ToUnixTimeSeconds()),
            ("$fiveHour", usage.FiveHour?.Utilization),
            ("$fiveHourResets", usage.FiveHour?.ResetsAt?.ToUnixTimeSeconds()),
            ("$sevenDay", usage.SevenDay?.Utilization),
            ("$sevenDayResets", usage.SevenDay?.ResetsAt?.ToUnixTimeSeconds()),
            ("$modelWindows", JsonSerializer.Serialize(usage.ModelWindows)));
    }

    public ClaudeUsage? LatestClaude()
    {
        using var connection = database.Open();
        using var command = Database.Command(connection, """
            SELECT ts, five_hour, five_hour_resets, seven_day, seven_day_resets, model_windows
            FROM claude_snapshots ORDER BY ts DESC LIMIT 1
            """);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;
        return new(
            DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)),
            ReadWindow(reader, 1),
            ReadWindow(reader, 3),
            JsonSerializer.Deserialize<Dictionary<string, UsageWindow>>(reader.GetString(5)) ?? []);
    }

    static UsageWindow? ReadWindow(SqliteDataReader reader, int column)
    {
        double? utilization = reader.IsDBNull(column) ? null : reader.GetDouble(column);
        DateTimeOffset? resetsAt = reader.IsDBNull(column + 1) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(column + 1));
        return utilization is null && resetsAt is null ? null : new(utilization, resetsAt);
    }

    public void Save(ActionsUsage usage)
    {
        using var connection = database.Open();
        Database.Execute(connection, """
            INSERT OR REPLACE INTO github_snapshots (ts, plan, included_amount, skus, resets_on)
            VALUES ($ts, $plan, $included, $skus, $resetsOn)
            """,
            ("$ts", usage.FetchedAt.ToUnixTimeSeconds()),
            ("$plan", usage.Plan),
            ("$included", usage.IncludedAmount),
            ("$skus", JsonSerializer.Serialize(usage.Skus)),
            ("$resetsOn", usage.ResetsOn.ToString("O")));
    }

    public ActionsUsage? LatestGitHub()
    {
        using var connection = database.Open();
        using var command = Database.Command(connection,
            "SELECT ts, plan, included_amount, skus, resets_on FROM github_snapshots ORDER BY ts DESC LIMIT 1");
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;
        return new(
            DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetDouble(2),
            JsonSerializer.Deserialize<Dictionary<string, SkuUsage>>(reader.GetString(3)) ?? [],
            DateOnly.ParseExact(reader.GetString(4), "O"));
    }
}
