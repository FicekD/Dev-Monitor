using System.Text.Json;
using DevMonitor.Core.Sensors;

namespace DevMonitor.Core;

public sealed class Settings
{
    public List<string> IncludedSensors { get; set; } = [];
    public List<string> ExcludedSensors { get; set; } = [];

    public bool IsRecorded(SensorInfo sensor)
    {
        if (IncludedSensors.Contains(sensor.Key))
            return true;
        if (ExcludedSensors.Contains(sensor.Key))
            return false;
        return sensor.Kind is SensorKind.Temperature or SensorKind.Fan or SensorKind.Control
            || (sensor.Kind == SensorKind.Load && sensor.Name is "CPU Total" or "GPU Core");
    }

    public static Settings Load(string path) =>
        File.Exists(path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new() : new();
}
