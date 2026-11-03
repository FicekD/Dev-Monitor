using DevMonitor.Core.Sensors;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;

namespace DevMonitor.Windows;

public sealed class LhmSensorSource : ISensorSource
{
    readonly Computer computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMotherboardEnabled = true,
        IsControllerEnabled = true,
        IsStorageEnabled = true,
    };
    readonly Dictionary<ISensor, SensorInfo> infos = [];

    public IReadOnlyList<string> Problems { get; }

    public LhmSensorSource()
    {
        var problems = new List<string>();
        if (!Environment.IsPrivilegedProcess)
            problems.Add("The app is not running as administrator, so most hardware sensors are unavailable.");
        using (var driver = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO"))
            if (driver is null)
                problems.Add("The PawnIO driver is not installed, so CPU temperature, power and clock sensors are unavailable. "
                    + "Install it with 'winget install namazso.PawnIO'.");
        Problems = problems;
        computer.Open();
    }

    public IReadOnlyList<SensorReading> Read()
    {
        var readings = new List<SensorReading>();
        foreach (var hardware in computer.Hardware)
            Collect(hardware, readings);
        return readings;
    }

    void Collect(IHardware hardware, List<SensorReading> readings)
    {
        hardware.Update();
        foreach (var subHardware in hardware.SubHardware)
            Collect(subHardware, readings);
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is not { } value || !float.IsFinite(value))
                continue;
            if (sensor.SensorType == SensorType.Temperature && value == 0)
                continue;
            if (!infos.TryGetValue(sensor, out var info))
            {
                info = new SensorInfo(sensor.Identifier.ToString(), sensor.Name, hardware.Name, Kind(sensor.SensorType));
                infos[sensor] = info;
            }
            readings.Add(new(info, value));
        }
    }

    static SensorKind Kind(SensorType type) => type switch
    {
        SensorType.Temperature => SensorKind.Temperature,
        SensorType.Fan => SensorKind.Fan,
        SensorType.Control => SensorKind.Control,
        SensorType.Load => SensorKind.Load,
        SensorType.Power => SensorKind.Power,
        SensorType.Clock => SensorKind.Clock,
        SensorType.Voltage => SensorKind.Voltage,
        _ => SensorKind.Other,
    };

    public void Dispose() => computer.Close();
}
