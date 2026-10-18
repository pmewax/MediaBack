using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MediaBack.Platform;



public sealed class KeyboardInterceptor : IDisposable
{
    private readonly Thread thread;
    private readonly HookProc callback;
    private readonly ManualResetEventSlim ready = new();
    private IntPtr hook;
    private uint threadId;
    private Exception? error;
    public volatile bool VolumeEnabled;
    public volatile bool MediaEnabled;
    public Func<int, uint, bool>? IsShortcut;
    public event Action<int>? KeyPressed;
    public KeyboardInterceptor()
    {
        callback = OnKey;
        thread = new Thread(Run) { IsBackground = true, Name = "MediaBack.MediaKeys" };
        thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException(L.Get("HookStartError"));
        if (error != null) throw error;
    }
    private void Run()
    {
        threadId = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
        hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) error = new Win32Exception(Marshal.GetLastWin32Error());
        ready.Set();
        if (error != null) return;
        try { while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0) { TranslateMessage(ref msg); DispatchMessage(ref msg); } }
        finally { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
    }
    private IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var key = Marshal.ReadInt32(lParam);
            if ((VolumeEnabled && key is >= 0xAD and <= 0xAF) || (MediaEnabled && key is 0xB0 or 0xB1 or 0xB3))
            {
                var modifiers = (Down(0x12) ? 1u : 0) | (Down(0x11) ? 2u : 0) | (Down(0x10) ? 4u : 0) | (Down(0x5B) || Down(0x5C) ? 8u : 0);
                if (IsShortcut?.Invoke(key, modifiers) == true) return CallNextHookEx(hook, code, wParam, lParam);
                if (wParam == (IntPtr)0x100 || wParam == (IntPtr)0x104) KeyPressed?.Invoke(key);
                return (IntPtr)1;
            }
        }
        return CallNextHookEx(hook, code, wParam, lParam);
    }
    public void Dispose()
    {
        VolumeEnabled = MediaEnabled = false;
        if (threadId != 0) PostThreadMessage(threadId, 0x12, IntPtr.Zero, IntPtr.Zero);
        if (Thread.CurrentThread != thread) thread.Join(1500);
        ready.Dispose();
    }
    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
    private static bool Down(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [StructLayout(LayoutKind.Sequential)] private struct Message { public IntPtr hwnd; public uint message; public UIntPtr wParam; public IntPtr lParam; public uint time; public int x, y; public uint privateData; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetMessage(out Message msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message msg, IntPtr hwnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message msg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref Message msg);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint id, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
}
