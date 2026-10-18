using MediaBack.UI;
using Forms = System.Windows.Forms;

namespace MediaBack.Platform;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly System.Drawing.Icon image;
    public event Action? SettingsRequested, PanelRequested, ExitRequested;
    public bool IsVisible => icon.Visible;
    public void SetVisible(bool visible) { if (icon.Visible != visible) icon.Visible = visible; }
    public TrayService(bool visible = true)
    {
        image = Branding.CreateTrayIcon();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(L.Get("TraySettings"), null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add(L.Get("ShowFlyout"), null, (_, _) => PanelRequested?.Invoke());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(L.Get("Quit"), null, (_, _) => ExitRequested?.Invoke());
        icon = new Forms.NotifyIcon { Text = L.Get("TrayTitle"), Icon = image, ContextMenuStrip = menu, Visible = visible };
        icon.DoubleClick += (_, _) => SettingsRequested?.Invoke();
        L.Changed += ApplyLanguage;
    }
    private void ApplyLanguage()
    {
        icon.Text = L.Get("TrayTitle");
        var items = icon.ContextMenuStrip!.Items;
        items[0].Text = L.Get("TraySettings"); items[1].Text = L.Get("ShowFlyout"); items[3].Text = L.Get("Quit");
    }
    internal string SettingsLabel => icon.ContextMenuStrip!.Items[0].Text ?? "";
    public void Dispose() { L.Changed -= ApplyLanguage; icon.Visible = false; icon.ContextMenuStrip?.Dispose(); icon.Dispose(); image.Dispose(); }
}
