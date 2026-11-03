using DevMonitor.Core.Claude;
using DevMonitor.Core.GitHub;
using DevMonitor.Core.Sensors;
using DevMonitor.Core.Storage;
using Serilog;

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
    static readonly ILogger Log = Serilog.Log.ForContext<Collector>();

    readonly SnapshotStore snapshots = new(database);
    readonly SensorStore sensorStore = new(database);
    volatile ISensorSource? sensors;
    DateTimeOffset lastAggregate = DateTimeOffset.MinValue;
    bool sensorsReported;

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
            foreach (var problem in sensors.Problems)
                Log.Warning("Hardware sensors are limited: {Problem}", problem);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Hardware sensors could not be started");
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
            Recovered("Claude usage", Claude.Error);
            Log.Debug("Claude usage is {FiveHour}% of the 5-hour and {Weekly}% of the weekly limit",
                usage.FiveHour?.Utilization, usage.SevenDay?.Utilization);
            Claude = Claude.Succeeded(usage, usage.FetchedAt);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            Failed("Claude usage", Claude.Error, exception);
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
            Recovered("GitHub Actions usage", GitHub.Error);
            Log.Debug("GitHub Actions usage is {Amount:0.00} USD of {Included} USD", usage.Amount, usage.IncludedAmount);
            GitHub = GitHub.Succeeded(usage, usage.FetchedAt);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            Failed("GitHub Actions usage", GitHub.Error, exception);
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
                var started = DateTimeOffset.UtcNow;
                sensorStore.Aggregate(now);
                lastAggregate = now;
                if (DateTimeOffset.UtcNow - started > TimeSpan.FromSeconds(1))
                    Log.Warning("Aggregating sensor history took {Duration}", DateTimeOffset.UtcNow - started);
            }
            if (!sensorsReported)
            {
                sensorsReported = true;
                Log.Information("Reading {Count} hardware sensors, recording {Recorded}",
                    readings.Count, readings.Count(reading => current.IsRecorded(reading.Sensor)));
            }
            Recovered("Hardware sensors", Sensors.Error);
            Sensors = Sensors.Succeeded(readings, now);
        }
        catch (Exception exception)
        {
            Failed("Hardware sensors", Sensors.Error, exception);
            Sensors = Sensors.Failed(exception.Message, now);
        }
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    static void Failed(string source, string? previousError, Exception exception)
    {
        if (exception.Message == previousError)
            return;
        var expected = exception is InvalidOperationException or HttpRequestException or TaskCanceledException;
        Log.Warning(expected ? null : exception, "{Source} could not be refreshed: {Error}", source, exception.Message);
    }

    static void Recovered(string source, string? previousError)
    {
        if (previousError is not null)
            Log.Information("{Source} is available again", source);
    }
}
