namespace DevMonitor.Core;

public sealed record SourceState<T>(
    TimeSpan Interval,
    T? Value = null,
    DateTimeOffset? UpdatedAt = null,
    string? Error = null,
    DateTimeOffset? ErrorAt = null) where T : class
{
    public bool IsStale(DateTimeOffset now) => UpdatedAt is null || now - UpdatedAt > Interval * 2;

    public SourceState<T> Succeeded(T value, DateTimeOffset at) =>
        this with { Value = value, UpdatedAt = at, Error = null, ErrorAt = null };

    public SourceState<T> Failed(string error, DateTimeOffset at) => this with { Error = error, ErrorAt = at };
}
