using System.IO;
using Microsoft.Win32;

namespace MediaBack.Platform;

public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool IsEnabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue("MediaBack") is string value && value.Length > 0; }
    }
    public static bool IsInstalled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\MediaBack");
            var folder = key?.GetValue("InstallFolder") as string;
            return folder != null && string.Equals(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar), AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }
    }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue("MediaBack", "\"" + Environment.ProcessPath + "\" --background");
        else key.DeleteValue("MediaBack", false);
    }
}
