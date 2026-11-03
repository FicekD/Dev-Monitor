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
installer/              Inno Setup script
scripts/installer.ps1   Publishes a self-contained build and runs Inno Setup
scripts/make-icon.py    Renders Assets/Icon.ico (Python with Pillow); small sizes use a simplified glyph
version.props           The product version, imported by every project and passed to the installer
```

The CLI fetches each source on demand, runs the collector in the foreground and queries stored history. Run `devmon` without arguments for the list of commands. It is meant for debugging and is not shipped with the app.

`DevMonitor.Core` targets plain `net10.0` and must not reference anything Windows-specific. Everything that only works on Windows, such as the LibreHardwareMonitor wrapper, DPAPI or Credential Manager access and Task Scheduler registration, lives in a Windows project or behind an interface. That keeps a Linux port down to a new sensor project and a few platform services.

## App structure

`DevMonitor.App` is organized by feature. `Claude/`, `GitHub/`, `Hardware/`, `Dashboard/` and `Configuration/` hold the views for each source and the settings page; `Shell/` holds the window, the page registry and `LiveView`; `Controls/` holds the shared elements; `Theme/` holds the tokens and styles.

- **Theme.** `Theme/Palette.axaml` defines every color once per theme variant (light and dark) together with the fonts, column widths and icons. Controls only reference these tokens through `DynamicResource`, so a theme change needs no code. The chart series colors are a validated colorblind-safe palette in fixed order.
- **Elements.** `InstrumentRow` (label, large value, a bar, detail text), `Meter` (usage bar with a pace tick showing how much of the window has passed), `RangeBar` (24-hour range with the current value), `Section` (title, freshness, error), `Notice`, `FieldRow`, `NavButton`, `LegendEntry`, `RangePicker` and `HistoryChart` get their look from control themes in `Theme/Controls.axaml`. Pages compose them and never style individual elements.
- **Live views.** A view derives from `LiveView`, which subscribes to the collector only while the view is on screen, refreshes once a second for freshness text and reloads history from SQLite on its own interval. Views are reusable, so the dashboard is just a composition of the source views. It shows usage in one column and temperatures in a second column when the window is wide enough.
- **Adding a source.** Add its client and snapshot storage to Core and a poll loop to `Collector`, write a summary view and a page view, add the summary to `DashboardPage` and the page to `Pages.Main`.

The window draws its own title strip and caption buttons instead of the system title bar. The UI scale setting applies one layout transform to the whole window, so every element scales and reflows together. Charts zoom by dragging across a time range and reset with a right click; the view re-reads the selected range from SQLite at full resolution, and on the Hardware page all charts follow the same zoom.

Window placement, the current page, chart ranges and hidden chart series are kept in `ui-state.json`. The normal window bounds are read from Windows when the window closes, so they stay correct while it is maximized. User choices such as the theme, UI scale and recorded sensors are in `settings.json`.

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

Sensor access needs administrator rights, so autostart is a Task Scheduler task named `DevMonitor` that runs `DevMonitor.exe --tray` at logon with the highest available privileges, no time limit and no battery conditions. This avoids a UAC prompt on every boot, which a Startup folder shortcut or Run registry key would cause. The task is created from XML in `Autostart.cs`. Settings can switch it on and off; when the app is not elevated, `schtasks` is started through UAC for that one change. The installer uses `DevMonitor.exe --enable-autostart` and `--disable-autostart`, which do the same without opening the app.

When the app runs without administrator rights, Settings offers "Restart as administrator". The single-instance signal is released before the elevated copy starts, so it does not hand off to the old process.

## Logging

Serilog writes to `%LOCALAPPDATA%\DevMonitor\logs\devmonitor-<date>.log`, one file per day, and deletes files older than 14 days. Debug builds log at Debug level and release builds at Information. The collector logs a source failure once when its error changes and once when it recovers, not on every poll. Avalonia's own warnings and unhandled exceptions go to the same log. The last 1,000 entries are also kept in memory for the log console, which the Log button in the sidebar opens at the bottom of the window.

## Development

`dotnet run --project src/DevMonitor.App --launch-profile Elevated` relaunches the built executable through UAC, so the whole feature set including sensors and the startup task can be tested without an elevated terminal. The elevated copy runs detached from the terminal; its output is in the log file and the log console. The `Tray` profile does the same and starts without opening the window, like autostart does. A startup task registered from a development build points at `buildin`, which Settings shows, so switch it off again after testing.

## Releases

Bump `version.props`, then run `scripts\installer.ps1`. It publishes a self-contained win-x64 build to `build\publish\installer` and writes `build\installer\DevMonitor-<version>-setup.exe`. The setup installs per machine to Program Files, offers the startup task, offers to install PawnIO through winget when it is missing, and removes the startup task on uninstall. Data in `%LOCALAPPDATA%\DevMonitor` is kept on uninstall.
