using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MediaBack.Domain;

namespace MediaBack.Platform;

internal static class BrowserSourceResolver
{
    public static bool IsBrowser(string id) => new[] { "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "yandexbrowser" }.Any(n => id.Contains(n, StringComparison.OrdinalIgnoreCase));
    public static string? Resolve(string sourceId, string title, params string?[] metadata)
    {
        if (!IsBrowser(sourceId)) return null;
        var exact = MediaSourceLabel.FromMetadata(metadata);
        if (exact != null) return exact;
        string? match = null;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            GetWindowThreadProcessId(hwnd, out var processId);
            try
            {
                using var process = Process.GetProcessById((int)processId);
                if (!sourceId.Contains(process.ProcessName, StringComparison.OrdinalIgnoreCase)) return true;
                var text = new StringBuilder(2048); GetWindowText(hwnd, text, text.Capacity);
                var site = MediaSourceLabel.FromMatchingWindow(title, text.ToString());
                if (site != null) { match = site; return false; }
            }
            catch (Exception) { }
            return true;
        }, IntPtr.Zero);
        return match;
    }
    private delegate bool WindowVisitor(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowVisitor visitor, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
}
