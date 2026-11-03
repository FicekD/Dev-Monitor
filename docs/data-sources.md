# Data sources

## Claude usage

There is no official API for subscription limits. Claude Code's `/usage` screen reads an undocumented endpoint, and the app uses the same one:

```
GET https://api.anthropic.com/api/oauth/usage
Authorization: Bearer <accessToken>
anthropic-beta: oauth-2025-04-20
```

The token is `claudeAiOauth.accessToken` in `~/.claude/.credentials.json` (on macOS it lives in the Keychain instead). The response contains `five_hour` and `seven_day` objects with a `utilization` percentage and a `resets_at` timestamp, and may contain extra per-model weekly windows. Check the field names against a live response before relying on them.

The app only reads the credentials file and never refreshes the token itself. Claude Code owns the token and rewrites that file when it renews it, so writing to it could log Claude Code out. When the token has expired because Claude Code has not been used for a while, the panel shows the last known values as stale.

Polling every 5 minutes is enough.

## GitHub Actions minutes

The legacy `GET /users/{username}/settings/billing/actions` endpoint, which reported included minutes directly, returns HTTP 410 since the account moved to the new billing platform (checked in November 2026). The app uses the newer endpoint, filtered to the current month:

```
GET /users/{username}/settings/billing/usage?year=2026&month=11
```

It returns `usageItems` with `product`, `sku`, `unitType`, `quantity` and `grossAmount`. The app sums items with product `actions` and unit type `Minutes` per SKU. The allowance is a dollar amount spent at each runner's price per minute, not a minute count with fixed multipliers: the Free plan includes $12, which is 2,000 minutes at the Linux price of $0.006, and a Windows minute costs $0.01 and a macOS minute $0.062. The API does not report the allowance, so it comes from the plan name returned by `GET /user` (`plan.name`) using a small table: Free has $12, and Pro is assumed to have $18 (3,000 Linux minutes). The total matched the billing page on github.com in November 2026. Storage is billed against a separate allowance and is not counted.

Only private repositories count against the allowance. The token is a fine-grained personal access token with read access to the "Plan" account permission, stored in Windows Credential Manager under `DevMonitor/GitHub`. For debugging, the `DEVMONITOR_GITHUB_TOKEN` environment variable takes precedence. Billing data lags by a few hours, so polling once an hour is enough.

## Hardware sensors

On Windows the app embeds `LibreHardwareMonitorLib` and reads sensors in-process. There is no separate monitoring program or service. CPU temperature, power and clock sensors require the PawnIO driver, which LibreHardwareMonitor does not ship, and all low-level sensors require the process to run as administrator. Without PawnIO those sensors are simply empty, so the app should say so instead of showing blank graphs. Without administrator rights the CPU temperature reads exactly 0 °C rather than being empty, so zero temperatures are discarded. NVIDIA GPU sensors work without elevation.

LibreHardwareMonitor exposes hundreds of sensors, most of them per-core clocks and voltages. By default the app records temperatures, fan speeds, fan duty and total CPU and GPU load. The selection is configurable.

On Linux, temperatures and fans are plain files under `/sys/class/hwmon` and can be read without root.
