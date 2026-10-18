using System.IO;
using System.Reflection;

namespace MediaBack.UI;

internal static class Branding
{
    public static System.Drawing.Icon CreateTrayIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MediaBack.BrandIcon") ?? throw new FileNotFoundException("MediaBack icon resource");
        using var icon = new System.Drawing.Icon(stream, 32, 32);
        return (System.Drawing.Icon)icon.Clone();
    }
}
