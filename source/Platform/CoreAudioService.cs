using System.Runtime.InteropServices;
using System.Windows.Threading;
using MediaBack.Domain;

namespace MediaBack.Platform;



public sealed class CoreAudioService : IAudioService
{
    private IMMDeviceEnumerator? enumerator;
    private IAudioEndpointVolume? endpoint;
    private string? deviceId;
    private DateTime nextDeviceCheck;
    private readonly Guid context = Guid.NewGuid();
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly EndpointNotifications endpointNotifications;
    private readonly DeviceNotifications deviceNotifications;
    private bool disposed;
    private int refreshQueued, deviceChangeQueued;
    public int Volume { get; private set; }
    public bool Muted { get; private set; }
    public bool Available { get; private set; }
    public event Action? Changed;
    public CoreAudioService()
    {
        endpointNotifications = new EndpointNotifications(() => QueueRefresh(false));
        deviceNotifications = new DeviceNotifications(() => QueueRefresh(true));
        Refresh();
    }
    private void QueueRefresh(bool deviceChanged)
    {
        if (disposed || dispatcher.HasShutdownStarted) return;
        if (deviceChanged) Interlocked.Exchange(ref deviceChangeQueued, 1);
        if (Interlocked.Exchange(ref refreshQueued, 1) != 0) return;
        dispatcher.BeginInvoke(() =>
        {
            Interlocked.Exchange(ref refreshQueued, 0);
            if (disposed) return;
            if (Interlocked.Exchange(ref deviceChangeQueued, 0) != 0) nextDeviceCheck = DateTime.MinValue;
            Refresh();
        });
    }
    public void Refresh()
    {
        if (disposed) return;
        var oldVolume = Volume; var oldMute = Muted; var oldAvailable = Available;
        try
        {
            if (endpoint == null || DateTime.UtcNow >= nextDeviceCheck)
            {
                if (enumerator == null)
                {
                    enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                    enumerator.RegisterEndpointNotificationCallback(deviceNotifications);
                }
                enumerator.GetDefaultAudioEndpoint(0, 1, out var device);
                try
                {
                    device.GetId(out var id);
                    if (endpoint == null || id != deviceId)
                    {
                        DetachEndpoint();
                        var iid = typeof(IAudioEndpointVolume).GUID;
                        device.Activate(ref iid, 23, IntPtr.Zero, out var obj);
                        endpoint = (IAudioEndpointVolume)obj;
                        endpoint.RegisterControlChangeNotify(endpointNotifications);
                        deviceId = id;
                    }
                }
                finally { Release(device); }
                nextDeviceCheck = DateTime.UtcNow.AddSeconds(10);
            }
            endpoint!.GetMasterVolumeLevelScalar(out var scalar);
            endpoint.GetMute(out var muted);
            Volume = (int)Math.Round(scalar * 100); Muted = muted; Available = true;
        }
        catch (COMException) { DetachEndpoint(); Available = false; }
        if (oldVolume != Volume || oldMute != Muted || oldAvailable != Available) Changed?.Invoke();
    }
    public void SetVolume(int volume)
    {
        try { if (endpoint != null) { var ctx = context; endpoint.SetMasterVolumeLevelScalar(Math.Clamp(volume, 0, 100) / 100f, ref ctx); } }
        catch (COMException) { }
        Refresh();
    }
    public void Adjust(int delta) { Refresh(); if (delta > 0 && Muted) ToggleMute(); SetVolume(Volume + delta); }
    public void ToggleMute()
    {
        Refresh();
        try { if (endpoint != null) { var ctx = context; endpoint.SetMute(!Muted, ref ctx); } }
        catch (COMException) { }
        Refresh();
    }
    private static void Release(object? obj) { if (obj != null && Marshal.IsComObject(obj)) Marshal.ReleaseComObject(obj); }
    private void DetachEndpoint()
    {
        if (endpoint == null) return;
        try { endpoint.UnregisterControlChangeNotify(endpointNotifications); } catch (COMException) { }
        Release(endpoint); endpoint = null;
    }
    public void Dispose()
    {
        disposed = true; DetachEndpoint();
        if (enumerator != null) { try { enumerator.UnregisterEndpointNotificationCallback(deviceNotifications); } catch (COMException) { } Release(enumerator); enumerator = null; }
    }
}

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] internal class MMDeviceEnumerator { }
[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    void EnumAudioEndpoints(int flow, uint state, out IntPtr devices);
    void GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
    void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    void RegisterEndpointNotificationCallback(IMMNotificationClient callback);
    void UnregisterEndpointNotificationCallback(IMMNotificationClient callback);
}
[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    void Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object obj);
    void OpenPropertyStore(uint mode, out IntPtr properties);
    void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    void GetState(out uint state);
}
[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    void RegisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
    void UnregisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
    void GetChannelCount(out uint count);
    void SetMasterVolumeLevel(float db, ref Guid context);
    void SetMasterVolumeLevelScalar(float level, ref Guid context);
    void GetMasterVolumeLevel(out float db);
    void GetMasterVolumeLevelScalar(out float level);
    void SetChannelVolumeLevel(uint channel, float db, ref Guid context);
    void SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
    void GetChannelVolumeLevel(uint channel, out float db);
    void GetChannelVolumeLevelScalar(uint channel, out float level);
    void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
    void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    void GetVolumeStepInfo(out uint step, out uint count);
    void VolumeStepUp(ref Guid context);
    void VolumeStepDown(ref Guid context);
    void QueryHardwareSupport(out uint mask);
    void GetVolumeRange(out float min, out float max, out float increment);
}
