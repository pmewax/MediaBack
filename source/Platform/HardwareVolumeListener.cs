using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MediaBack.Platform;



public sealed class HardwareVolumeListener : IDisposable
{
    private readonly HwndSource source;
    private readonly Dictionary<IntPtr, uint> absoluteVolumes = new();
    public event Action? VolumeInput;
    public HardwareVolumeListener(HwndSource source)
    {
        this.source = source;
        var devices = new[] { new RawDevice { UsagePage = 0x0C, Usage = 1, Flags = 0x100, Target = source.Handle } };
        if (!RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RawDevice>())) throw new Win32Exception(Marshal.GetLastWin32Error());
        source.AddHook(Message);
    }
    private IntPtr Message(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != 0xFF) return IntPtr.Zero;
        try { Read(lParam); } catch (Exception ex) when (ex is not OutOfMemoryException) { }

        return IntPtr.Zero;
    }
    private void Read(IntPtr handle)
    {
        var header = (uint)Marshal.SizeOf<RawHeader>(); uint size = 0;
        if (GetRawInputData(handle, 0x10000003, IntPtr.Zero, ref size, header) == uint.MaxValue || size < header + 8 || size > 65536) return;
        var data = Marshal.AllocHGlobal((int)size); IntPtr preparsed = IntPtr.Zero;
        try
        {
            if (GetRawInputData(handle, 0x10000003, data, ref size, header) == uint.MaxValue) return;
            var raw = Marshal.PtrToStructure<RawHeader>(data); if (raw.Type != 2 || raw.Device == IntPtr.Zero) return;
            var reportSize = (uint)Marshal.ReadInt32(data, (int)header); var count = (uint)Marshal.ReadInt32(data, (int)header + 4);
            if (reportSize == 0 || reportSize > 8192 || count > 64 || header + 8L + (long)reportSize * count > size) return;
            uint capsSize = 0;
            if (GetRawInputDeviceInfo(raw.Device, 0x20000005, IntPtr.Zero, ref capsSize) == uint.MaxValue || capsSize == 0 || capsSize > 65536) return;
            preparsed = Marshal.AllocHGlobal((int)capsSize);
            if (GetRawInputDeviceInfo(raw.Device, 0x20000005, preparsed, ref capsSize) == uint.MaxValue) return;
            var input = false;
            for (var n = 0; n < count; n++)
            {
                var report = new byte[reportSize]; Marshal.Copy(IntPtr.Add(data, checked((int)(header + 8 + n * reportSize))), report, 0, report.Length);
                var usages = new ushort[64]; uint usageCount = 64;
                if (HidP_GetUsages(0, 0x0C, 0, usages, ref usageCount, preparsed, report, reportSize) == 0x110000)
                    input |= usages.Take((int)Math.Min(usageCount, 64)).Any(u => u is 0xE9 or 0xEA or 0xE2);
                foreach (var usage in new ushort[] { 0xE9, 0xEA, 0xE2 })
                    if (HidP_GetUsageValue(0, 0x0C, 0, usage, out var value, preparsed, report, reportSize) == 0x110000 && value != 0) input = true;
                if (HidP_GetUsageValue(0, 0x0C, 0, 0xE0, out var absolute, preparsed, report, reportSize) == 0x110000)
                {
                    if (absoluteVolumes.TryGetValue(raw.Device, out var previous) && previous != absolute) input = true;
                    if (absoluteVolumes.Count > 64) absoluteVolumes.Clear();
                    absoluteVolumes[raw.Device] = absolute;
                }
            }
            if (input) VolumeInput?.Invoke();
        }
        finally { if (preparsed != IntPtr.Zero) Marshal.FreeHGlobal(preparsed); Marshal.FreeHGlobal(data); }
    }
    public void Dispose()
    {
        source.RemoveHook(Message);
        RegisterRawInputDevices([new RawDevice { UsagePage = 0x0C, Usage = 1, Flags = 1 }], 1, (uint)Marshal.SizeOf<RawDevice>());
        absoluteVolumes.Clear();
    }
    [StructLayout(LayoutKind.Sequential)] private struct RawDevice { public ushort UsagePage, Usage; public uint Flags; public IntPtr Target; }
    [StructLayout(LayoutKind.Sequential)] private struct RawHeader { public uint Type, Size; public IntPtr Device, WParam; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(RawDevice[] devices, uint count, uint size);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(IntPtr raw, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW")] private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);
    [DllImport("hid.dll")] private static extern int HidP_GetUsages(int type, ushort page, ushort collection, [Out] ushort[] usages, ref uint count, IntPtr preparsed, byte[] report, uint length);
    [DllImport("hid.dll")] private static extern int HidP_GetUsageValue(int type, ushort page, ushort collection, ushort usage, out uint value, IntPtr preparsed, byte[] report, uint length);
}
