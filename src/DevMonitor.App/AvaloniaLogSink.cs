using Avalonia.Logging;
using Serilog;

namespace DevMonitor.App;

sealed class AvaloniaLogSink : ILogSink
{
    const string InputAfterClose = "PlatformImpl is null, couldn't handle input.";

    public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
        Log(level, area, source, messageTemplate, []);

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        if (!IsEnabled(level, area))
            return;
        var serilogLevel = messageTemplate == InputAfterClose ? Serilog.Events.LogEventLevel.Debug : (Serilog.Events.LogEventLevel)level;
        Serilog.Log.ForContext("SourceContext", $"Avalonia.{area}").Write(serilogLevel, messageTemplate, propertyValues);
    }
}
