using System.Runtime.InteropServices;

namespace MediaBack.Platform;

[ComVisible(true), Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioEndpointVolumeCallback { [PreserveSig] int OnNotify(IntPtr notification); }

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class EndpointNotifications(Action changed) : IAudioEndpointVolumeCallback
{
    public int OnNotify(IntPtr notification) { changed(); return 0; }
}

[ComVisible(true), Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMNotificationClient
{
    [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, uint state);
    [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
    [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
    [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string? id);
    [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
}
[StructLayout(LayoutKind.Sequential)] public struct PropertyKey { public Guid Format; public uint Id; }
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class DeviceNotifications(Action changed) : IMMNotificationClient
{
    public int OnDeviceStateChanged(string id, uint state) { changed(); return 0; }
    public int OnDeviceAdded(string id) { changed(); return 0; }
    public int OnDeviceRemoved(string id) { changed(); return 0; }
    public int OnDefaultDeviceChanged(int flow, int role, string? id) { if (flow == 0 && role == 1) changed(); return 0; }
    public int OnPropertyValueChanged(string id, PropertyKey key) => 0;
}
