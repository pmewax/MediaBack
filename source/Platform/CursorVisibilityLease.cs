using System.Runtime.InteropServices;

namespace MediaBack.Platform;



internal sealed class CursorVisibilityLease : IDisposable
{
    private readonly Func<bool, int> show;
    private int increments;
    internal int IncrementCount => increments;
    public CursorVisibilityLease() : this(ShowCursor) { }
    internal CursorVisibilityLease(Func<bool, int> show) => this.show = show;
    public void Acquire()
    {
        if (increments != 0) return;
        int value;
        do { value = show(true); increments++; } while (value < 0 && increments < 16);
    }
    public void Dispose()
    {
        while (increments > 0) { show(false); increments--; }
    }
    [DllImport("user32.dll")] private static extern int ShowCursor([MarshalAs(UnmanagedType.Bool)] bool visible);
}
