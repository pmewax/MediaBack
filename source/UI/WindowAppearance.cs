using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MediaBack.UI;

internal static class WindowAppearance
{
    public static void UseDarkFrame(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var enabled = 1; DwmSetWindowAttribute(hwnd, 20, ref enabled, 4);
            var rounded = 2; DwmSetWindowAttribute(hwnd, 33, ref rounded, 4);
        };
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
