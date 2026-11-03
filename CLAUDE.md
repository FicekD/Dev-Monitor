# Dev Monitor

Personal tray dashboard for Claude usage limits, GitHub Actions minutes and hardware temperature history. Read `docs/scope.md` before adding features and `docs/architecture.md` before adding files or projects. `docs/data-sources.md` documents the external endpoints and their quirks.

## Stack

.NET 10, Avalonia (Fluent theme), LiveCharts2, SQLite via `Microsoft.Data.Sqlite`, `LibreHardwareMonitorLib` on Windows.

## Commands

```bash
rtk dotnet build
rtk dotnet run --project src/DevMonitor.App
rtk dotnet run --project src/DevMonitor.Cli -- <command>   # debugging CLI, run without a command for help
rtk dotnet publish src/DevMonitor.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Rules

- `DevMonitor.Core` stays platform-neutral (`net10.0`). Windows-only code goes in a Windows project or behind an interface.
- Never write to `~/.claude/.credentials.json` or refresh the Claude token. Claude Code owns it; only read it.
- The Claude usage endpoint is undocumented. Keep its parsing in one place and treat unknown or missing fields as "data unavailable", not as a crash.
- Each data source fails independently. Show stale or missing data with its last update time instead of hiding it.
- The window is created on open and disposed on close. Do not keep view models, charts or history in memory while it is closed.
- Sensors need administrator rights and the PawnIO driver. When they are unavailable the app must still run and say why sensor data is missing.
- Prefer code-behind or simple view models over MVVM frameworks and heavy abstractions. Add a NuGet package only after asking.
- Package versions live only in `Directory.Packages.props`; project files reference packages without a version. Build output goes to `./build` through `ArtifactsPath` in `Directory.Build.props`, for example `build/bin/DevMonitor.Cli/debug/devmon.exe`.
