namespace DevMonitor.Core.Storage;

public sealed record UserEvent(long Id, DateTimeOffset Time, string Kind, string? Note);

public sealed class EventStore(Database database)
{
    public long Add(DateTimeOffset time, string kind, string? note)
    {
        using var connection = database.Open();
        using var command = Database.Command(connection,
            "INSERT INTO events (ts, kind, note) VALUES ($ts, $kind, $note) RETURNING id",
            ("$ts", time.ToUnixTimeSeconds()), ("$kind", kind), ("$note", note));
        return (long)command.ExecuteScalar()!;
    }

    public bool Delete(long id)
    {
        using var connection = database.Open();
        return Database.Execute(connection, "DELETE FROM events WHERE id = $id", ("$id", id)) > 0;
    }

    public IReadOnlyList<UserEvent> List()
    {
        using var connection = database.Open();
        using var command = Database.Command(connection, "SELECT id, ts, kind, note FROM events ORDER BY ts");
        using var reader = command.ExecuteReader();
        var events = new List<UserEvent>();
        while (reader.Read())
            events.Add(new(reader.GetInt64(0), DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(1)),
                reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        return events;
    }
}
