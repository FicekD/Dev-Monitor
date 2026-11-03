using System.Diagnostics;
using System.Net;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

namespace DevMonitor.Windows;

public static class Autostart
{
    const string TaskName = "DevMonitor";

    public static string? RegisteredExecutable()
    {
        using var process = Process.Start(new ProcessStartInfo("schtasks.exe", $"/Query /TN \"{TaskName}\" /XML")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            return null;
        var command = Regex.Match(output, "<Command>(.*?)</Command>");
        return command.Success ? WebUtility.HtmlDecode(command.Groups[1].Value) : null;
    }

    public static void Enable(string executable)
    {
        var definition = Path.Combine(Path.GetTempPath(), $"devmonitor-task-{Guid.NewGuid():N}.xml");
        File.WriteAllText(definition, TaskDefinition(executable), Encoding.Unicode);
        try
        {
            RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{definition}\" /F");
        }
        finally
        {
            File.Delete(definition);
        }
    }

    public static void Disable()
    {
        if (RegisteredExecutable() is not null)
            RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
    }

    static void RunSchtasks(string arguments)
    {
        using var process = Elevation.IsElevated
            ? Process.Start(new ProcessStartInfo("schtasks.exe", arguments) { UseShellExecute = false, CreateNoWindow = true })
            : Elevation.Start("schtasks.exe", arguments, hidden: true);
        if (process is null)
            throw new InvalidOperationException("Administrator approval was declined.");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Task Scheduler rejected the change (schtasks exit code {process.ExitCode}).");
    }

    static string TaskDefinition(string executable)
    {
        var user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
        var command = SecurityElement.Escape(executable);
        var directory = SecurityElement.Escape(Path.GetDirectoryName(executable)!);
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Starts Dev Monitor in the tray when you sign in.</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{user}</UserId>
                  <Delay>PT10S</Delay>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>false</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{command}</Command>
                  <Arguments>--tray</Arguments>
                  <WorkingDirectory>{directory}</WorkingDirectory>
                </Exec>
              </Actions>
            </Task>
            """;
    }
}
