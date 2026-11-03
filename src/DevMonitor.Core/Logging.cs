using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace DevMonitor.Core;

public sealed record LogEntry(DateTimeOffset Time, LogEventLevel Level, string Source, string Message, string? Exception);

public sealed class LogBuffer : ILogEventSink
{
    const int Capacity = 1000;
    readonly Queue<LogEntry> entries = new();

    public event Action<LogEntry>? Added;

    public void Emit(LogEvent logEvent)
    {
        var source = logEvent.Properties.TryGetValue(Constants.SourceContextPropertyName, out var value)
            && value is ScalarValue { Value: string context }
                ? context[(context.LastIndexOf('.') + 1)..]
                : "";
        var entry = new LogEntry(logEvent.Timestamp, logEvent.Level, source, logEvent.RenderMessage(), logEvent.Exception?.ToString());
        lock (entries)
        {
            entries.Enqueue(entry);
            while (entries.Count > Capacity)
                entries.Dequeue();
        }
        Added?.Invoke(entry);
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (entries)
            return entries.ToList();
    }

    public void Clear()
    {
        lock (entries)
            entries.Clear();
    }
}

sealed class LevelNameEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory) =>
        logEvent.AddOrUpdateProperty(factory.CreateProperty("LevelName", Logging.LevelName(logEvent.Level)));
}

public static class Logging
{
    public static LogBuffer Buffer { get; } = new();

    public static string LevelName(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "Verbose",
        LogEventLevel.Debug => "Debug",
        LogEventLevel.Information => "Info",
        LogEventLevel.Warning => "Warning",
        LogEventLevel.Error => "Error",
        _ => "Fatal",
    };

    public static void Configure(string directory, LogEventLevel minimumLevel)
    {
        Directory.CreateDirectory(directory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.WithProperty(Constants.SourceContextPropertyName, "DevMonitor")
            .Enrich.With<LevelNameEnricher>()
            .WriteTo.File(
                Path.Combine(directory, "devmonitor-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: null,
                retainedFileTimeLimit: TimeSpan.FromDays(14),
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {LevelName,-7} {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.Sink(Buffer)
            .CreateLogger();
    }
}
