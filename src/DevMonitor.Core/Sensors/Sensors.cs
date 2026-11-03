namespace DevMonitor.Core.Sensors;

public enum SensorKind { Temperature, Fan, Control, Load, Power, Clock, Voltage, Other }

public sealed record SensorInfo(string Key, string Name, string Hardware, SensorKind Kind)
{
    public string Unit => Kind switch
    {
        SensorKind.Temperature => "°C",
        SensorKind.Fan => "RPM",
        SensorKind.Control or SensorKind.Load => "%",
        SensorKind.Power => "W",
        SensorKind.Clock => "MHz",
        SensorKind.Voltage => "V",
        _ => "",
    };
}

public readonly record struct SensorReading(SensorInfo Sensor, double Value);

public interface ISensorSource : IDisposable
{
    IReadOnlyList<string> Problems { get; }

    IReadOnlyList<SensorReading> Read();
}
