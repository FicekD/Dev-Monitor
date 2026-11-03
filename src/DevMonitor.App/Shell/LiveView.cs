using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace DevMonitor.App.Shell;

public abstract class LiveView(AppServices services) : UserControl
{
    DispatcherTimer? clock;
    DateTimeOffset lastReload;
    bool reloading;
    bool reloadPending;

    protected AppServices Services { get; } = services;

    protected virtual TimeSpan ReloadInterval => TimeSpan.FromMinutes(1);

    protected abstract void Refresh(DateTimeOffset now);

    protected virtual Task ReloadAsync() => Task.CompletedTask;

    protected async void Reload()
    {
        if (reloading)
        {
            reloadPending = true;
            return;
        }
        reloading = true;
        try
        {
            do
            {
                reloadPending = false;
                lastReload = DateTimeOffset.UtcNow;
                await ReloadAsync();
            }
            while (reloadPending);
        }
        finally
        {
            reloading = false;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Services.Collector.Changed += OnCollectorChanged;
        clock = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Tick());
        clock.Start();
        Reload();
        Refresh(DateTimeOffset.UtcNow);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Services.Collector.Changed -= OnCollectorChanged;
        clock?.Stop();
        clock = null;
        base.OnDetachedFromVisualTree(e);
    }

    void OnCollectorChanged() => Dispatcher.UIThread.Post(() =>
    {
        if (clock is not null)
            Refresh(DateTimeOffset.UtcNow);
    });

    void Tick()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - lastReload >= ReloadInterval)
            Reload();
        Refresh(now);
    }
}
