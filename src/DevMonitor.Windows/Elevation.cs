using System.ComponentModel;
using System.Diagnostics;

namespace DevMonitor.Windows;

public static class Elevation
{
    const int Cancelled = 1223;

    public static bool IsElevated => Environment.IsPrivilegedProcess;

    public static Process? Start(string file, string arguments, bool hidden = false)
    {
        try
        {
            return Process.Start(new ProcessStartInfo(file, arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
            });
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == Cancelled)
        {
            return null;
        }
    }

    public static string Quote(IEnumerable<string> arguments) =>
        string.Join(' ', arguments.Select(argument => argument.Contains(' ') ? $"\"{argument}\"" : argument));
}
