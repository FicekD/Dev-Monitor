# Scope

Dev Monitor is a single-user tool for one PC. It starts with the system, stays in the tray, and opens a window on demand. It should be cheap to leave running all the time and should never need attention unless something is actually wrong.

## Version 1

- **Claude usage.** Percentage used of the 5-hour and weekly limits, with reset times. The tray icon tooltip shows the 5-hour figure so a glance is enough.
- **GitHub Actions minutes.** Usage this month against the plan's included amount ($12 on the Free plan, which is 2,000 Linux minutes), with the date the counter resets.
- **Hardware history.** Temperatures, fan speeds, fan duty and CPU/GPU load recorded in the background, with graphs for the last 24 hours, 7 days, 30 days and the whole history.
- **Autostart.** Starts elevated at logon through a Task Scheduler task, without a UAC prompt.
- **Single instance.** Launching the app again opens the window of the instance that is already running.
- **Staleness.** Every panel shows when its data was last refreshed, and clearly marks data that is out of date instead of silently showing old numbers.

## Later

- **Cleaning alert.** Dust shows up as higher temperatures at the same load and fan speed, or as fans spinning faster to hold the same temperature. The app compares recent behaviour within matching load ranges against a baseline taken after the last cleaning, which the user marks with a button. Room temperature is the main source of error, so comparisons favour periods close in time or season. Version 1 records load and fan data alongside temperatures so this can be built on real history.
- **Notifications.** Optional toasts when a Claude limit is close, when Actions minutes run low, or when a temperature stays unusually high.
- **Linux sensors.** A second sensor implementation reading `/sys/class/hwmon`. The rest of the app is already portable.

## Out of scope

- Fan control or any other writes to hardware.
- Monitoring other machines or exposing data over the network.
- Multiple Claude or GitHub accounts.
- macOS sensor support.
