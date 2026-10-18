using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MediaBack.Platform;

namespace MediaBack.UI;

public partial class OverlayWindow
{
    private const long NoActivate = 0x08000000L;
    private readonly CursorVisibilityLease cursorLease = new();
    private IntPtr previousForeground;
    private IntPtr arrow;
    public bool IsInteracting { get; private set; }
    private IntPtr HandleMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x21 && !IsInteracting) { handled = true; return (IntPtr)3; }
        if (message == 0x20 && IsVisible && (settings.ShowCursorOnHover || IsInteracting)
            && ((lParam.ToInt64() & 0xFFFF) == 1 || IsMouseCaptureWithin))
        {
            EnsurePanelCursor(); handled = true; return (IntPtr)1;
        }
        return IntPtr.Zero;
    }
    private void EnsurePanelCursor()
    {
        if (!IsVisible || (!settings.ShowCursorOnHover && !IsInteracting)) return;
        cursorLease.Acquire();
        if (arrow == IntPtr.Zero) arrow = LoadCursor(IntPtr.Zero, (IntPtr)32512);
        SetCursor(arrow);
    }
    private void ReleasePanelCursor() => cursorLease.Dispose();
    public void ToggleInteraction()
    {
        if (closed) return;
        if (IsInteracting) { EndInteraction(true); BeginHide(); return; }
        previousForeground = GetForegroundWindow();
        Reveal();
        IsInteracting = true;
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowLongPtr(hwnd, -20, (IntPtr)(GetWindowLongPtr(hwnd, -20).ToInt64() & ~NoActivate));


        SetForegroundWindow(hwnd); Activate();
        if (GetForegroundWindow() != hwnd) { EndInteraction(false); return; }
        Focusable = true; Focus();
        GameHint.Visibility = Visibility.Visible;
        Mouse.Capture(this, CaptureMode.SubTree);
        EnsurePanelCursor();
        RestartTimer();
    }
    internal static bool CanRestoreForeground(IntPtr foreground, IntPtr panel, IntPtr previous)
        => foreground == panel && previous != IntPtr.Zero && previous != panel;
    private void EndInteraction(bool restoreFocus)
    {
        if (!IsInteracting) { ReleasePanelCursor(); return; }
        IsInteracting = false; GameHint.Visibility = Visibility.Collapsed; Focusable = false;
        if (IsMouseCaptureWithin) Mouse.Capture(null);
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowLongPtr(hwnd, -20, (IntPtr)(GetWindowLongPtr(hwnd, -20).ToInt64() | NoActivate));
        ReleasePanelCursor();
        var previous = previousForeground; previousForeground = IntPtr.Zero;
        if (restoreFocus && CanRestoreForeground(GetForegroundWindow(), hwnd, previous) && IsWindowVisible(previous)) SetForegroundWindow(previous);
    }
    private void InteractionKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (IsInteracting && e.Key == Key.Escape) { e.Handled = true; EndInteraction(true); BeginHide(); }
    }
    private void InteractionMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsInteracting) return;
        var point = e.GetPosition(this);
        if (point.X >= 0 && point.Y >= 0 && point.X <= ActualWidth && point.Y <= ActualHeight) return;
        e.Handled = true; EndInteraction(true); BeginHide();
    }
    private void InteractionCaptureLost(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!IsInteracting || IsMouseCaptureWithin) return;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            if (IsInteracting && IsVisible && Mouse.Captured == null && GetForegroundWindow() == new WindowInteropHelper(this).Handle)
                Mouse.Capture(this, CaptureMode.SubTree);
        });
    }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);
    [DllImport("user32.dll")] private static extern IntPtr SetCursor(IntPtr cursor);
}
