using System.CommandLine;
using System.Text;
using DevMonitor.Core;
using DevMonitor.Core.Claude;
using DevMonitor.Core.GitHub;
using DevMonitor.Windows;

namespace DevMonitor.Cli;

static class SourceCommands
{
    public static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static IEnumerable<Command> Create()
    {
        var raw = new Option<bool>("--raw") { Description = "Print the raw endpoint response" };
        var claude = new Command("claude", "Claude 5-hour and weekly limits") { raw };
        claude.SetAction((result, cancellation) => Claude(result.GetValue(raw), cancellation));
        yield return claude;

        var set = new Command("set", "Store a GitHub token in Credential Manager, read from stdin or typed");
        set.SetAction(_ => SetGitHubToken());
        var clear = new Command("clear", "Remove the stored GitHub token");
        clear.SetAction(_ => ClearGitHubToken());
        var github = new Command("github", "GitHub Actions usage this month against the included amount")
        {
            new Command("token", "Manage the stored GitHub token") { set, clear },
        };
        github.SetAction((_, cancellation) => GitHub(cancellation));
        yield return github;

        var all = new Option<bool>("--all") { Description = "Include sensors that are not recorded" };
        var sensors = new Command("sensors", "Current hardware sensor values") { all };
        sensors.SetAction(result => Sensors(result.GetValue(all)));
        yield return sensors;
    }

    static async Task Claude(bool raw, CancellationToken cancellation)
    {
        var response = await new ClaudeUsageClient(Http, AppPaths.ClaudeCredentials).FetchRawAsync(cancellation);
        if (raw)
        {
            Console.WriteLine(response);
            return;
        }
        foreach (var line in Format.Claude(ClaudeUsage.Parse(response, DateTimeOffset.UtcNow)))
            Console.WriteLine(line);
    }

    static async Task GitHub(CancellationToken cancellation)
    {
        var usage = await new GitHubActionsClient(Http, new CredentialStore()).FetchAsync(cancellation);
        foreach (var line in Format.GitHub(usage))
            Console.WriteLine(line);
    }

    static void SetGitHubToken()
    {
        var token = Console.IsInputRedirected ? Console.In.ReadLine() : ReadHidden("GitHub token: ");
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("No token was given.");
        new CredentialStore().Set(GitHubActionsClient.TokenSecretName, token.Trim());
        Console.WriteLine("The GitHub token is stored in Credential Manager.");
    }

    static void ClearGitHubToken()
    {
        var deleted = new CredentialStore().Delete(GitHubActionsClient.TokenSecretName);
        Console.WriteLine(deleted ? "The GitHub token was removed." : "No GitHub token was stored.");
    }

    static string ReadHidden(string prompt)
    {
        Console.Write(prompt);
        var text = new StringBuilder();
        for (var key = Console.ReadKey(true); key.Key != ConsoleKey.Enter; key = Console.ReadKey(true))
        {
            if (key.Key == ConsoleKey.Backspace && text.Length > 0)
                text.Length--;
            else if (!char.IsControl(key.KeyChar))
                text.Append(key.KeyChar);
        }
        Console.WriteLine();
        return text.ToString();
    }

    static void Sensors(bool all)
    {
        var settings = Settings.Load(AppPaths.Settings);
        using var source = new LhmSensorSource();
        foreach (var problem in source.Problems)
            Console.WriteLine($"! {problem}");

        source.Read();
        Thread.Sleep(1000);
        var readings = source.Read();
        var shown = 0;
        foreach (var group in readings.GroupBy(reading => reading.Sensor.Hardware))
        {
            var lines = group
                .Where(reading => all || settings.IsRecorded(reading.Sensor))
                .Select(reading => $"  {(settings.IsRecorded(reading.Sensor) ? '*' : ' ')} {reading.Sensor.Kind,-11} "
                    + $"{reading.Value,9:0.##} {reading.Sensor.Unit,-4} {reading.Sensor.Name,-28} {reading.Sensor.Key}")
                .ToList();
            if (lines.Count == 0)
                continue;
            Console.WriteLine(group.Key);
            lines.ForEach(Console.WriteLine);
            shown += lines.Count;
        }
        var recorded = readings.Count(reading => settings.IsRecorded(reading.Sensor));
        Console.WriteLine($"{readings.Count} sensors with values, {recorded} recorded (*), {shown} shown.");
    }
}
