using DevMonitor.Core;
using DevMonitor.Core.Claude;
using DevMonitor.Core.GitHub;
using DevMonitor.Core.Storage;
using DevMonitor.Windows;

namespace DevMonitor.App;

public sealed class AppServices : IDisposable
{
    readonly CancellationTokenSource stop = new();
    readonly Task collecting;

    public HttpClient Http { get; } = new() { Timeout = TimeSpan.FromSeconds(30) };
    public Database Database { get; }
    public SensorStore Sensors { get; }
    public SnapshotStore Snapshots { get; }
    public ISecretStore Secrets { get; } = new CredentialStore();
    public Collector Collector { get; }
    public UiState Ui { get; } = UiState.Load();

    public Settings Settings => Collector.Settings;

    public event Action? SettingsChanged;

    public AppServices()
    {
        Database = new Database(AppPaths.Database);
        Sensors = new SensorStore(Database);
        Snapshots = new SnapshotStore(Database);
        Collector = new Collector(
            Database,
            new ClaudeUsageClient(Http, AppPaths.ClaudeCredentials),
            new GitHubActionsClient(Http, Secrets),
            () => new LhmSensorSource(),
            Settings.Load(AppPaths.Settings));
        collecting = Collector.RunAsync(stop.Token);
    }

    public void UpdateSettings(Func<Settings, Settings> change)
    {
        var settings = change(Settings);
        settings.Save(AppPaths.Settings);
        Collector.Settings = settings;
        SettingsChanged?.Invoke();
    }

    public void RefreshGitHub() => _ = Collector.PollGitHubAsync(stop.Token);

    public void Dispose()
    {
        stop.Cancel();
        try
        {
            collecting.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
        }
        Ui.Save();
        Http.Dispose();
    }
}
