using System.CommandLine;
using System.CommandLine.Parsing;
using DevMonitor.Core;
using DevMonitor.Core.Claude;
using DevMonitor.Core.GitHub;
using DevMonitor.Core.Storage;
using DevMonitor.Windows;

namespace DevMonitor.Cli;

static class StorageCommands
{
    public static IEnumerable<Command> Create()
    {
        var seconds = new Option<int?>("--for") { Description = "Stop after this many seconds instead of waiting for Ctrl+C" };
        seconds.Validators.Add(result => RequirePositive(result, result.GetValueOrDefault<int?>()));
        var collect = new Command("collect", "Run the collector in the foreground and write to the database") { seconds };
        collect.SetAction((result, cancellation) => Collect(result.GetValue(seconds), cancellation));
        yield return collect;

        var status = new Command("status", "Latest stored data and database statistics");
        status.SetAction(_ => Status());
        yield return status;

        var sensor = new Argument<string?>("sensor")
        {
            Description = "Sensor id or key; lists recorded sensors when omitted",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var range = new Option<string>("--range") { Description = "Time range", DefaultValueFactory = _ => "24h" };
        range.AcceptOnlyFromAmong("24h", "7d", "30d", "all");
        var points = new Option<int>("--points") { Description = "Number of points", DefaultValueFactory = _ => 24 };
        points.Validators.Add(result => RequirePositive(result, result.GetValueOrDefault<int>()));
        var history = new Command("history", "Stored history of a sensor") { sensor, range, points };
        history.SetAction(result => History(result.GetValue(sensor), result.GetValue(range)!, result.GetValue(points)));
        yield return history;

        var kind = new Argument<string>("kind") { Description = "Event kind, such as 'cleaned'" };
        var note = new Argument<string[]>("note") { Description = "Optional note", Arity = ArgumentArity.ZeroOrMore };
        var add = new Command("add", "Record an event") { kind, note };
        add.SetAction(result => AddEvent(result.GetValue(kind)!, result.GetValue(note)!));
        var list = new Command("list", "List recorded events");
        list.SetAction(_ => ListEvents());
        var id = new Argument<long>("id");
        var delete = new Command("delete", "Delete an event") { id };
        delete.SetAction(result => DeleteEvent(result.GetValue(id)));
        yield return new Command("event", "User events used as baselines") { add, list, delete };
    }

    static void RequirePositive(OptionResult result, int? value)
    {
        if (value < 1)
            result.AddError($"{result.Option.Name} must be at least 1.");
    }

    static async Task Collect(int? seconds, CancellationToken stop)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stop);
        if (seconds is { } limit)
            cancellation.CancelAfter(TimeSpan.FromSeconds(limit));

        var collector = new Collector(
            new Database(AppPaths.Database),
            new ClaudeUsageClient(SourceCommands.Http, AppPaths.ClaudeCredentials),
            new GitHubActionsClient(SourceCommands.Http, new CredentialStore()),
            () => new LhmSensorSource(),
            Settings.Load(AppPaths.Settings));

        var gate = new object();
        collector.Changed += () =>
        {
            lock (gate)
            {
                var claude = collector.Claude.Value is { } usage
                    ? $"{usage.FiveHour?.Utilization:0.#}% / {usage.SevenDay?.Utilization:0.#}%"
                    : "-";
                var github = collector.GitHub.Value is { } actions ? $"${actions.Amount:0.00}" : "-";
                var recorded = collector.Sensors.Value?.Count(reading => collector.Settings.IsRecorded(reading.Sensor)).ToString() ?? "-";
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] claude {claude} | github {github} | sensors {recorded} recorded");
                foreach (var (name, error) in new[]
                {
                    ("claude", collector.Claude.Error),
                    ("github", collector.GitHub.Error),
                    ("sensors", collector.Sensors.Error),
                })
                    if (error is not null)
                        Console.WriteLine($"           {name}: {error}");
            }
        };

        try
        {
            await collector.RunAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
        foreach (var problem in collector.SensorProblems)
            Console.WriteLine($"! {problem}");
        Console.WriteLine($"Claude:  {Format.State(collector.Claude)}");
        Console.WriteLine($"GitHub:  {Format.State(collector.GitHub)}");
        Console.WriteLine($"Sensors: {Format.State(collector.Sensors)}");
    }

    static void Status()
    {
        var database = new Database(AppPaths.Database);
        var snapshots = new SnapshotStore(database);

        Console.WriteLine($"Database {AppPaths.Database} ({new FileInfo(AppPaths.Database).Length / 1024.0 / 1024.0:0.0} MB)");
        Console.WriteLine();
        if (snapshots.LatestClaude() is { } claude)
        {
            Console.WriteLine($"Claude, fetched {Format.Age(claude.FetchedAt)}");
            foreach (var line in Format.Claude(claude))
                Console.WriteLine("  " + line);
        }
        else
            Console.WriteLine("Claude: no snapshot stored");

        if (snapshots.LatestGitHub() is { } github)
        {
            Console.WriteLine($"GitHub, fetched {Format.Age(github.FetchedAt)}");
            foreach (var line in Format.GitHub(github))
                Console.WriteLine("  " + line);
        }
        else
            Console.WriteLine("GitHub: no snapshot stored");

        Console.WriteLine();
        using var connection = database.Open();
        foreach (var table in new[] { "sensors", "readings", "readings_minute", "readings_hour", "claude_snapshots", "github_snapshots", "events" })
        {
            using var count = Database.Command(connection, $"SELECT COUNT(*) FROM {table}");
            Console.WriteLine($"{table,-17} {(long)count.ExecuteScalar()!,12:N0} rows");
        }
        using var latest = Database.Command(connection, "SELECT MAX(ts) FROM readings");
        if (latest.ExecuteScalar() is long last)
            Console.WriteLine($"Last sensor reading {Format.Age(DateTimeOffset.FromUnixTimeSeconds(last))}");
    }

    static void History(string? key, string range, int points)
    {
        var store = new SensorStore(new Database(AppPaths.Database));
        var sensors = store.Sensors();
        if (key is null)
        {
            foreach (var sensor in sensors)
                Console.WriteLine($"{sensor.Id,4}  {sensor.Info.Kind,-11} {sensor.Info.Hardware} / {sensor.Info.Name}  {sensor.Info.Key}");
            Console.WriteLine($"{sensors.Count} sensors recorded.");
            return;
        }

        var selected = sensors.FirstOrDefault(sensor => sensor.Info.Key == key || sensor.Id.ToString() == key)
            ?? throw new InvalidOperationException($"No recorded sensor matches '{key}'.");
        var to = DateTimeOffset.UtcNow;
        var from = range switch
        {
            "24h" => to.AddHours(-24),
            "7d" => to.AddDays(-7),
            "30d" => to.AddDays(-30),
            _ => store.FirstReading(selected.Id) ?? to,
        };

        var unit = selected.Info.Unit;
        Console.WriteLine($"{selected.Info.Hardware} / {selected.Info.Name}, {range}");
        var history = store.History(selected.Id, from, to, points);
        foreach (var point in history)
            Console.WriteLine($"{Format.Time(point.Time)}  min {point.Min,8:0.##}  avg {point.Avg,8:0.##}  max {point.Max,8:0.##} {unit}");
        Console.WriteLine($"{history.Count} points.");
    }

    static void AddEvent(string kind, string[] note)
    {
        var id = new EventStore(new Database(AppPaths.Database))
            .Add(DateTimeOffset.UtcNow, kind, note.Length == 0 ? null : string.Join(' ', note));
        Console.WriteLine($"Event {id} recorded.");
    }

    static void ListEvents()
    {
        var events = new EventStore(new Database(AppPaths.Database)).List();
        foreach (var e in events)
            Console.WriteLine($"{e.Id,4}  {Format.Time(e.Time)}  {e.Kind}  {e.Note}");
        Console.WriteLine($"{events.Count} events.");
    }

    static int DeleteEvent(long eventId)
    {
        var deleted = new EventStore(new Database(AppPaths.Database)).Delete(eventId);
        Console.WriteLine(deleted ? $"Event {eventId} deleted." : $"There is no event {eventId}.");
        return deleted ? 0 : 1;
    }
}
