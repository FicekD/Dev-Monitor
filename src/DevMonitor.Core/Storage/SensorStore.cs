using DevMonitor.Core.Sensors;
using Microsoft.Data.Sqlite;

namespace DevMonitor.Core.Storage;

public sealed record StoredSensor(long Id, SensorInfo Info);

public readonly record struct HistoryPoint(DateTimeOffset Time, double Min, double Avg, double Max);

public sealed class SensorStore(Database database)
{
    public static readonly TimeSpan RawRetention = TimeSpan.FromDays(7);
    public static readonly TimeSpan MinuteRetention = TimeSpan.FromDays(90);

    readonly Dictionary<string, long> sensorIds = [];

    public void Record(DateTimeOffset at, IEnumerable<SensorReading> readings)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        using var insert = Database.Command(connection,
            "INSERT OR REPLACE INTO readings (sensor_id, ts, value) VALUES ($id, $ts, $value)",
            ("$id", 0L), ("$ts", at.ToUnixTimeSeconds()), ("$value", 0.0));
        foreach (var reading in readings)
        {
            insert.Parameters["$id"].Value = SensorId(connection, reading.Sensor);
            insert.Parameters["$value"].Value = reading.Value;
            insert.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    long SensorId(SqliteConnection connection, SensorInfo sensor)
    {
        if (sensorIds.TryGetValue(sensor.Key, out var id))
            return id;
        using var command = Database.Command(connection, """
            INSERT INTO sensors (key, name, hardware, kind) VALUES ($key, $name, $hardware, $kind)
            ON CONFLICT (key) DO UPDATE SET name = excluded.name, hardware = excluded.hardware, kind = excluded.kind
            RETURNING id
            """,
            ("$key", sensor.Key), ("$name", sensor.Name), ("$hardware", sensor.Hardware), ("$kind", sensor.Kind.ToString()));
        id = (long)command.ExecuteScalar()!;
        sensorIds[sensor.Key] = id;
        return id;
    }

    public void Aggregate(DateTimeOffset now)
    {
        var seconds = now.ToUnixTimeSeconds();
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        Rollup(connection, "readings", "readings_minute", "MIN(value), AVG(value), MAX(value)", 60, seconds / 60 * 60);
        Rollup(connection, "readings_minute", "readings_hour", "MIN(min), AVG(avg), MAX(max)", 3600, seconds / 3600 * 3600);
        Database.Execute(connection,
            "DELETE FROM readings WHERE sensor_id IN (SELECT id FROM sensors) AND ts < $before",
            ("$before", (now - RawRetention).ToUnixTimeSeconds()));
        Database.Execute(connection,
            "DELETE FROM readings_minute WHERE sensor_id IN (SELECT id FROM sensors) AND ts < $before",
            ("$before", (now - MinuteRetention).ToUnixTimeSeconds()));
        transaction.Commit();
    }

    static void Rollup(SqliteConnection connection, string source, string target, string columns, long bucket, long end)
    {
        var watermark = target + "_until";
        Database.Execute(connection, $"""
            INSERT OR REPLACE INTO {target} (sensor_id, ts, min, avg, max)
            SELECT sensor_id, ts / {bucket} * {bucket}, {columns} FROM {source}
            WHERE sensor_id IN (SELECT id FROM sensors)
              AND ts >= COALESCE((SELECT value FROM meta WHERE key = $watermark), 0) AND ts < $end
            GROUP BY sensor_id, ts / {bucket}
            """, ("$watermark", watermark), ("$end", end));
        Database.Execute(connection,
            "INSERT OR REPLACE INTO meta (key, value) VALUES ($watermark, $end)",
            ("$watermark", watermark), ("$end", end));
    }

    public IReadOnlyList<StoredSensor> Sensors()
    {
        using var connection = database.Open();
        using var command = Database.Command(connection,
            "SELECT id, key, name, hardware, kind FROM sensors ORDER BY hardware, kind, name");
        using var reader = command.ExecuteReader();
        var sensors = new List<StoredSensor>();
        while (reader.Read())
            sensors.Add(new(reader.GetInt64(0), new SensorInfo(
                reader.GetString(1), reader.GetString(2), reader.GetString(3), Enum.Parse<SensorKind>(reader.GetString(4)))));
        return sensors;
    }

    public DateTimeOffset? FirstReading(long sensorId)
    {
        using var connection = database.Open();
        using var command = Database.Command(connection, """
            SELECT MIN(first) FROM (
                SELECT MIN(ts) AS first FROM readings_hour WHERE sensor_id = $id
                UNION ALL SELECT MIN(ts) FROM readings_minute WHERE sensor_id = $id
                UNION ALL SELECT MIN(ts) FROM readings WHERE sensor_id = $id)
            """, ("$id", sensorId));
        return command.ExecuteScalar() is long first ? DateTimeOffset.FromUnixTimeSeconds(first) : null;
    }

    public IReadOnlyList<HistoryPoint> History(long sensorId, DateTimeOffset from, DateTimeOffset to, int points = 1000)
    {
        var now = DateTimeOffset.UtcNow;
        var bucket = Math.Max(5, (long)(to - from).TotalSeconds / Math.Max(1, points));
        const string range = "sensor_id = $id AND ts >= $from AND ts < $to";
        const string raw = $"SELECT ts, value AS min, value AS avg, value AS max FROM readings WHERE {range}";
        const string rawTail = $"{raw} AND ts >= COALESCE((SELECT value FROM meta WHERE key = 'readings_minute_until'), 0)";
        const string minute = $"SELECT ts, min, avg, max FROM readings_minute WHERE {range}";
        const string minuteTail = $"{minute} AND ts >= COALESCE((SELECT value FROM meta WHERE key = 'readings_hour_until'), 0)";
        const string hour = $"SELECT ts, min, avg, max FROM readings_hour WHERE {range}";
        var source =
            bucket < 60 && from >= now - RawRetention ? raw
            : bucket < 3600 && from >= now - MinuteRetention ? $"{minute} UNION ALL {rawTail}"
            : $"{hour} UNION ALL {minuteTail} UNION ALL {rawTail}";

        using var connection = database.Open();
        using var command = Database.Command(connection, $"""
            SELECT ts / {bucket} * {bucket} AS bucket, MIN(min), AVG(avg), MAX(max) FROM ({source})
            GROUP BY bucket ORDER BY bucket
            """, ("$id", sensorId), ("$from", from.ToUnixTimeSeconds()), ("$to", to.ToUnixTimeSeconds()));
        using var reader = command.ExecuteReader();
        var history = new List<HistoryPoint>();
        while (reader.Read())
            history.Add(new(DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)),
                reader.GetDouble(1), reader.GetDouble(2), reader.GetDouble(3)));
        return history;
    }
}
