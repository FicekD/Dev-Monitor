namespace DevMonitor.Core;

public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DevMonitor");

    public static string Database => Path.Combine(DataDirectory, "devmonitor.db");

    public static string Settings => Path.Combine(DataDirectory, "settings.json");

    public static string ClaudeCredentials => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", ".credentials.json");
}
