using Microsoft.Data.Sqlite;

namespace DevMonitor.Core.Storage;

public sealed class Database
{
    const string Schema = """
        PRAGMA journal_mode = WAL;
        CREATE TABLE IF NOT EXISTS sensors (
            id INTEGER PRIMARY KEY,
            key TEXT NOT NULL UNIQUE,
            name TEXT NOT NULL,
            hardware TEXT NOT NULL,
            kind TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS readings (
            sensor_id INTEGER NOT NULL, ts INTEGER NOT NULL, value REAL NOT NULL,
            PRIMARY KEY (sensor_id, ts)) WITHOUT ROWID;
        CREATE TABLE IF NOT EXISTS readings_minute (
            sensor_id INTEGER NOT NULL, ts INTEGER NOT NULL, min REAL NOT NULL, avg REAL NOT NULL, max REAL NOT NULL,
            PRIMARY KEY (sensor_id, ts)) WITHOUT ROWID;
        CREATE TABLE IF NOT EXISTS readings_hour (
            sensor_id INTEGER NOT NULL, ts INTEGER NOT NULL, min REAL NOT NULL, avg REAL NOT NULL, max REAL NOT NULL,
            PRIMARY KEY (sensor_id, ts)) WITHOUT ROWID;
        CREATE TABLE IF NOT EXISTS claude_snapshots (
            ts INTEGER PRIMARY KEY,
            five_hour REAL, five_hour_resets INTEGER,
            seven_day REAL, seven_day_resets INTEGER,
            model_windows TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS github_snapshots (
            ts INTEGER PRIMARY KEY,
            plan TEXT NOT NULL,
            included_amount REAL,
            skus TEXT NOT NULL,
            resets_on TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS events (
            id INTEGER PRIMARY KEY,
            ts INTEGER NOT NULL,
            kind TEXT NOT NULL,
            note TEXT);
        CREATE TABLE IF NOT EXISTS meta (
            key TEXT PRIMARY KEY,
            value INTEGER NOT NULL);
        """;

    readonly string connectionString;

    public Database(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var connection = Open();
        Execute(connection, Schema);
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    public static int Execute(SqliteConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = Command(connection, sql, parameters);
        return command.ExecuteNonQuery();
    }

    public static SqliteCommand Command(SqliteConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }
}
