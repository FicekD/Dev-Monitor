# Dev Monitor

A small personal dashboard that lives in the system tray. It shows how much of the Claude 5-hour and weekly limits is used and when they reset, how many GitHub Actions minutes are left this month, and a long-term history of hardware temperatures and fan speeds.

It is built with .NET 10 and Avalonia. Sensor readings come from LibreHardwareMonitor, which on Windows needs the [PawnIO](https://github.com/namazso/PawnIO.Setup) driver (`winget install namazso.PawnIO`) and administrator rights.

See [docs/scope.md](docs/scope.md) for what the app does and does not do, and [docs/architecture.md](docs/architecture.md) for how it is put together.
