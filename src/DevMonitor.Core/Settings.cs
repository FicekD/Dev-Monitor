using System.Text.Json;
using System.Text.Json.Serialization;
using DevMonitor.Core.Sensors;

namespace DevMonitor.Core;

[JsonConverter(typeof(JsonStringEnumConverter<ThemePreference>))]
public enum ThemePreference { System, Light, Dark }

public sealed record Settings
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public IReadOnlyList<string> IncludedSensors { get; init; } = [];
    public IReadOnlyList<string> ExcludedSensors { get; init; } = [];
    public ThemePreference Theme { get; init; } = ThemePreference.System;
    public int UiScale { get; init; } = 100;

    public bool IsRecorded(SensorInfo sensor) =>
        IncludedSensors.Contains(sensor.Key) || (!ExcludedSensors.Contains(sensor.Key) && IsRecordedByDefault(sensor));

    public static bool IsRecordedByDefault(SensorInfo sensor) =>
        sensor.Kind is SensorKind.Temperature or SensorKind.Fan or SensorKind.Control
        || (sensor.Kind == SensorKind.Load && sensor.Name is "CPU Total" or "GPU Core");

    public Settings WithRecorded(SensorInfo sensor, bool recorded)
    {
        var included = IncludedSensors.Where(key => key != sensor.Key).ToList();
        var excluded = ExcludedSensors.Where(key => key != sensor.Key).ToList();
        if (recorded != IsRecordedByDefault(sensor))
            (recorded ? included : excluded).Add(sensor.Key);
        return this with { IncludedSensors = included, ExcludedSensors = excluded };
    }

    public static Settings Load(string path) =>
        File.Exists(path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), JsonOptions) ?? new() : new();

    public void Save(string path) => JsonFile.Write(path, JsonSerializer.Serialize(this, JsonOptions));
}
