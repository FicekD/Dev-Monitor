# Architecture

## Stack

- .NET 10 (LTS), published as one self-contained executable.
- Avalonia with the Fluent theme for the window and tray icon.
- LiveCharts2 for graphs.
- SQLite through `Microsoft.Data.Sqlite` for storage.
- `LibreHardwareMonitorLib` for Windows sensors.

## Layout

```
DevMonitor.slnx
src/
  DevMonitor.App/       Avalonia app: tray icon, window, view models, startup and single-instance handling
  DevMonitor.Cli/       devmon.exe, a debugging CLI over the same API the app uses
  DevMonitor.Core/      Claude and GitHub clients, storage, aggregation, sensor and secret interfaces, collector
  DevMonitor.Windows/   LibreHardwareMonitor sensor source and Credential Manager secret store
docs/
```

The CLI fetches each source on demand, runs the collector in the foreground and queries stored history. Run `devmon` without arguments for the list of commands. It is meant for debugging and is not shipped with the app.

`DevMonitor.Core` targets plain `net10.0` and must not reference anything Windows-specific. Everything that only works on Windows, such as the LibreHardwareMonitor wrapper, DPAPI or Credential Manager access and Task Scheduler registration, lives in a Windows project or behind an interface. That keeps a Linux port down to a new sensor project and a few platform services.

## Runtime model

The process has two parts with very different lifetimes.

The **collector** runs for as long as the app runs. It polls each source on its own interval (sensors every 5 seconds, Claude every 5 minutes, GitHub every hour), writes results to SQLite, and keeps the latest values in memory for the tray tooltip. A failure in one source is recorded with its timestamp and does not affect the others.

The **window** exists only while it is open. It is created when the user opens it from the tray and fully disposed when closed, so the idle app keeps only the collector and tray icon in memory. The window reads history from SQLite and subscribes to the collector for live updates while it is visible.

Starting a second copy of the executable signals the running instance to open its window and then exits.

## Storage

Everything lives in `%LOCALAPPDATA%\DevMonitor\`: the SQLite database, a settings file and logs.

Sensor data is kept at three resolutions so that any graph loads roughly a thousand points:

| Table | Resolution | Kept for |
|---|---|---|
| raw readings | 5 seconds | 7 days |
| minute aggregates | 1 minute (min, avg, max) | 90 days |
| hour aggregates | 1 hour (min, avg, max) | forever |

Aggregation and cleanup run in the collector once a minute. Sensors are stored in their own table keyed by LibreHardwareMonitor's stable identifier, so readings reference a small integer instead of repeating names.

Claude and GitHub results are stored as timestamped snapshots, which costs almost nothing and allows usage history graphs later. User events such as "cleaned the PC" go in an events table that the cleaning alert will use as baselines.

## Startup and privileges

Sensor access needs administrator rights, so the app is registered as a Task Scheduler task that runs at logon with highest privileges. This avoids a UAC prompt on every boot, which a Startup folder shortcut or Run registry key would cause. The app can create and remove this task from its settings.

During development, sensors only work when the IDE or terminal runs as administrator. Everything else works without elevation.
