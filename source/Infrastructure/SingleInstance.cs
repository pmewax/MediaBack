using System.Security.Cryptography;
using System.Text;

namespace MediaBack.Infrastructure;

public sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle? show, stop;
    private readonly RegisteredWaitHandle? showWait, stopWait;
    public bool IsPrimary { get; }
    private static string Key(string profile) => "Local\\MediaBack." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(profile.ToUpperInvariant())))[..16];
    public SingleInstance(string profile, Action openSettings, Action exit)
    {
        var key = Key(profile);
        mutex = new Mutex(true, key, out var first); IsPrimary = first;
        if (!first) { Signal(key + ".Show"); return; }
        show = new(false, EventResetMode.AutoReset, key + ".Show");
        stop = new(false, EventResetMode.AutoReset, key + ".Stop");
        showWait = ThreadPool.RegisterWaitForSingleObject(show, (_, _) => openSettings(), null, -1, false);
        stopWait = ThreadPool.RegisterWaitForSingleObject(stop, (_, _) => exit(), null, -1, false);
    }
    private static void Signal(string name) { try { using var signal = EventWaitHandle.OpenExisting(name); signal.Set(); } catch (WaitHandleCannotBeOpenedException) { } }
    public static void StopExisting(string profile)
    {
        var key = Key(profile); Signal(key + ".Stop");
        try { using var existing = Mutex.OpenExisting(key); if (existing.WaitOne(TimeSpan.FromSeconds(5))) existing.ReleaseMutex(); }
        catch (WaitHandleCannotBeOpenedException) { }
        catch (AbandonedMutexException) { }
    }
    public void Dispose()
    {
        showWait?.Unregister(null); stopWait?.Unregister(null); show?.Dispose(); stop?.Dispose();
        if (IsPrimary) mutex.ReleaseMutex(); mutex.Dispose();
    }
}
