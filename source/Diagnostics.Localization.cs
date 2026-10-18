using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MediaBack.Domain;
using MediaBack.Platform;
using MediaBack.UI;

namespace MediaBack;

internal static partial class Diagnostics
{
    private static async Task RunLanguageChecksAsync(SettingsWindow window, Settings config, BitmapSource image, string output, Action<string, bool> check)
    {
        check("RU and EN catalogs have identical complete key sets", L.Russian.Count >= 220 && L.Russian.Keys.Order().SequenceEqual(L.English.Keys.Order()) && L.English.Values.All(s => !string.IsNullOrWhiteSpace(s)));
        check("English catalog has no Russian UI text except the language selector", L.English.Where(x => x.Key != "Language").All(x => !Regex.IsMatch(x.Value, "[А-Яа-яЁё]")));
        var language = (ComboBox)window.FindName("LanguageBox");
        using var tray = new TrayService(false);
        using var registry = new ShortcutRegistry((_, _, _) => false, _ => { });
        registry.Apply(new Settings());
        language.SelectedIndex = 1;
        window.UpdateSources([], null, null, true);
        await Task.Delay(220); window.UpdateLayout();
        check("Language picker changes configuration and existing window title immediately", config.Language == AppLanguage.English && L.Language == AppLanguage.English && window.Title == "MediaBack — settings");
        check("Existing tray and shortcut errors switch to English", tray.SettingsLabel == "MediaBack settings" && registry.Error!.Contains("reserved by Windows"));
        check("Source fallback and status change to English", ((TextBlock)window.FindName("TrackStatus")).Text.StartsWith("Open a player") && ((ComboBox)window.FindName("SourceBox")).Items[0].ToString() == "Automatic · music");
        check("Volume label uses the English unit", ((TextBlock)window.FindName("StepLabel")).Text.StartsWith("Volume step"));
        var radios = Tree(window).OfType<RadioButton>().Where(x => x.GroupName == "Navigation").ToArray();
        foreach (var nav in radios)
        {
            nav.IsChecked = true; await Task.Delay(200); window.UpdateLayout();
            ((ScrollViewer)window.FindName("PageScroll")).ScrollToTop();
            var stale = Tree(window).SelectMany(UiText).Where(x => Regex.IsMatch(x, "[А-Яа-яЁё]") && x is not ("Русский · RU" or "Language / Язык")).ToArray();
            check("All UI labels/tooltips are English on page " + nav.Tag, stale.Length == 0);
            var supportNav = (RadioButton)window.FindName("SupportNav");
            var footer = (FrameworkElement)window.FindName("SidebarFooter");
            check("All sidebar entries remain above the footer on page " + nav.Tag,
                supportNav.TranslatePoint(new Point(0, supportNav.ActualHeight), window).Y <= footer.TranslatePoint(new Point(), window).Y + .5);
            SaveVisual(window, Path.Combine(output, "settings-en-" + nav.Tag + ".png"));
        }
        var store = new SettingsStore(Path.Combine(output, "language-profile"));
        store.Save(config);
        check("English, cursor and smoothing preferences survive reload", store.Load().Language == AppLanguage.English && store.Load().ShowCursorOnHover && store.Load().SmoothVolume);
        var dialog = new SupportDialog(true, _ => { }); dialog.Show(); await Task.Delay(200);
        check("New support greeting uses English", ((TextBlock)dialog.FindName("DialogTitle")).Text == "Hi! Good to see you" && dialog.Title == "MediaBack — support the author");
        SaveVisual(dialog, Path.Combine(output, "greeting-en.png"));
        dialog.SelectCoin("TRX");
        check("Wallet title, network and copy action use English without changing the address", ((TextBlock)dialog.FindName("DialogSubtitle")).Text == "Tron · native TRX" && ((Button)dialog.FindName("CopyAddressButton")).Content.ToString() == "Copy address" && ((TextBox)dialog.FindName("WalletAddress")).Text == SupportCatalog.Wallets[0].Address);
        Click(dialog, "CopyAddressButton");
        check("English clipboard confirmation is localized", ((Button)dialog.FindName("CopyAddressButton")).Content.ToString() == "✓  Address copied");
        L.SetLanguage(AppLanguage.Russian);
        check("Already-open support dialog switches back to Russian", ((TextBlock)dialog.FindName("DialogSubtitle")).Text == "Tron · нативный TRX" && ((Button)dialog.FindName("CopyAddressButton")).Content.ToString() == "Копировать адрес");
        dialog.Close();
        language.SelectedIndex = 1;
        var crop = new CropWindow(image, 2.5); crop.Show(); await Task.Delay(180); crop.UpdateLayout();
        check("Crop title and dynamic zoom label use English", crop.Title == "Crop background" && ((TextBlock)crop.FindName("ZoomLabel")).Text.StartsWith("Zoom"));
        SaveVisual(crop, Path.Combine(output, "crop-en.png")); crop.Close();
        language.SelectedIndex = 0; await Task.Delay(200); window.UpdateLayout();
        check("Switching back restores RU across settings and tray", config.Language == AppLanguage.Russian && window.Title == "MediaBack — настройки" && tray.SettingsLabel == "Настроить MediaBack" && ((TextBlock)window.FindName("StepLabel")).Text.StartsWith("Шаг громкости"));
        var old = System.Text.Json.JsonSerializer.Deserialize<Settings>("{}")!;
        check("Older profiles keep Russian and gain the independent game shortcut", old.Language == AppLanguage.Russian && old.GameInteractionShortcut == new HotkeyBinding(0x4D) && old.GetShortcuts().Count() == 5);
        old.Language = (AppLanguage)99; old.Normalize(); check("Invalid stored language recovers to Russian", old.Language == AppLanguage.Russian);
    }
    private static IEnumerable<DependencyObject> Tree(DependencyObject parent)
    {
        yield return parent;
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>()) foreach (var item in Tree(child)) yield return item;
    }
    private static IEnumerable<string> UiText(DependencyObject obj)
    {
        if (obj is TextBlock text) yield return text.Text;
        if (obj is ContentControl { Content: string content }) yield return content;
        if (obj is FrameworkElement { ToolTip: string tip }) yield return tip;
        if (obj is Window window) yield return window.Title;
        var name = AutomationProperties.GetName(obj); if (name.Length > 0) yield return name;
    }
}
