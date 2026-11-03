using DevMonitor.Core.Claude;
using DevMonitor.Core.GitHub;
using DevMonitor.Core.Sensors;
using DevMonitor.Core.Storage;

namespace DevMonitor.Core;

public sealed class Collector(
    Database database,
    ClaudeUsageClient claude,
    GitHubActionsClient github,
    Func<ISensorSource>? createSensors,
    Settings settings)
{
    public static readonly TimeSpan SensorInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan ClaudeInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan GitHubInterval = TimeSpan.FromHours(1);
    static readonly TimeSpan AggregateInterval = TimeSpan.FromMinutes(1);

    readonly SnapshotStore snapshots = new(database);
    readonly SensorStore sensorStore = new(database);
    volatile ISensorSource? sensors;
    DateTimeOffset lastAggregate = DateTimeOffset.MinValue;

    public Settings Settings { get; set; } = settings;

    public SourceState<ClaudeUsage> Claude { get; private set; } = new(ClaudeInterval);
    public SourceState<ActionsUsage> GitHub { get; private set; } = new(GitHubInterval);
    public SourceState<IReadOnlyList<SensorReading>> Sensors { get; private set; } = new(SensorInterval);

    public IReadOnlyList<string> SensorProblems =>
        createSensors is null ? ["Hardware sensors are not supported on this platform."] : sensors?.Problems ?? [];

    public event Action? Changed;

    public Task RunAsync(CancellationToken cancellationToken)
    {
        if (snapshots.LatestClaude() is { } lastClaude)
            Claude = Claude with { Value = lastClaude, UpdatedAt = lastClaude.FetchedAt };
        if (snapshots.LatestGitHub() is { } lastGitHub)
            GitHub = GitHub with { Value = lastGitHub, UpdatedAt = lastGitHub.FetchedAt };

        var loops = new List<Task>
        {
            Loop(ClaudeInterval, PollClaudeAsync, cancellationToken),
            Loop(GitHubInterval, PollGitHubAsync, cancellationToken),
        };
        if (createSensors is not null)
            loops.Add(Task.Run(() => RunSensorsAsync(createSensors, cancellationToken), cancellationToken));
        return Task.WhenAll(loops);
    }

    static Task Loop(TimeSpan interval, Func<CancellationToken, Task> poll, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(interval);
            do
                await poll(cancellationToken);
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }, cancellationToken);

    async Task RunSensorsAsync(Func<ISensorSource> create, CancellationToken cancellationToken)
    {
        try
        {
            sensors = create();
        }
        catch (Exception exception)
        {
            Sensors = Sensors.Failed(exception.Message, DateTimeOffset.UtcNow);
            Changed?.Invoke();
            return;
        }
        using (sensors)
            await Loop(SensorInterval, _ => PollSensors(), cancellationToken);
    }

    public async Task PollClaudeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var usage = await claude.FetchAsync(cancellationToken);
            snapshots.Save(usage);
            Claude = Claude.Succeeded(usage, usage.FetchedAt);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            Claude = Claude.Failed(exception.Message, DateTimeOffset.UtcNow);
        }
        Changed?.Invoke();
    }

    public async Task PollGitHubAsync(CancellationToken cancellationToken)
    {
        try
        {
            var usage = await github.FetchAsync(cancellationToken);
            snapshots.Save(usage);
            GitHub = GitHub.Succeeded(usage, usage.FetchedAt);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            GitHub = GitHub.Failed(exception.Message, DateTimeOffset.UtcNow);
        }
        Changed?.Invoke();
    }

    Task PollSensors()
    {
        var now = DateTimeOffset.UtcNow;
        try
        {
            var readings = sensors!.Read();
            var current = Settings;
            sensorStore.Record(now, readings.Where(reading => current.IsRecorded(reading.Sensor)));
            if (now - lastAggregate >= AggregateInterval)
            {
                sensorStore.Aggregate(now);
                lastAggregate = now;
            }
            Sensors = Sensors.Succeeded(readings, now);
        }
        catch (Exception exception)
        {
            Sensors = Sensors.Failed(exception.Message, now);
        }
        Changed?.Invoke();
        return Task.CompletedTask;
    }
}
