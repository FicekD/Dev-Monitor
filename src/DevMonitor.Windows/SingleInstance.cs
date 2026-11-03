using System.Security.AccessControl;
using System.Security.Principal;

namespace DevMonitor.Windows;

public sealed class SingleInstance : IDisposable
{
    readonly EventWaitHandle signal;
    readonly RegisteredWaitHandle registration;

    public event Action? Activated;

    SingleInstance(EventWaitHandle signal)
    {
        this.signal = signal;
        registration = ThreadPool.RegisterWaitForSingleObject(signal, (_, _) => Activated?.Invoke(), null, Timeout.Infinite, false);
    }

    public static SingleInstance? TryAcquire(string name)
    {
        var fullName = $@"Local\{name}";
        if (EventWaitHandleAcl.TryOpenExisting(fullName, EventWaitHandleRights.Modify, out var existing))
        {
            using (existing)
                existing.Set();
            return null;
        }

        var security = new EventWaitHandleSecurity();
        security.AddAccessRule(new EventWaitHandleAccessRule(
            WindowsIdentity.GetCurrent().User!,
            EventWaitHandleRights.Modify | EventWaitHandleRights.Synchronize,
            AccessControlType.Allow));
        var created = EventWaitHandleAcl.Create(false, EventResetMode.AutoReset, fullName, out var isNew, security);
        if (isNew)
            return new SingleInstance(created);
        using (created)
            created.Set();
        return null;
    }

    public void Dispose()
    {
        registration.Unregister(null);
        signal.Dispose();
    }
}
