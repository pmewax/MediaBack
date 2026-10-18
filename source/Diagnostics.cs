using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using MediaBack.Domain;
using MediaBack.Platform;
using MediaBack.UI;
using MediaBack.Infrastructure;

namespace MediaBack;



internal static partial class Diagnostics
{
    public static async Task RunAsync(string? reportPath)
    {
        L.SetLanguage(AppLanguage.Russian);
        reportPath ??= Path.Combine(AppContext.BaseDirectory, "self-test.json");
        var checks = new List<object>();
        var failed = 0;
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) failed++; }
        try
        {
            static MediaItem Item(string id, bool playing = true, bool music = true, bool known = false) => new(id, id, "Track", "Artist", playing, music, known, true, true, true, TimeSpan.Zero, TimeSpan.FromMinutes(3));
            var music = Item("spotify.exe", known: true); var game = Item("game.exe", music: false); var video = Item("browser.exe", music: false);
            var settings = new Settings();
            Check("Music wins over game", SessionSelector.Select([game, music], settings, null)?.SourceId == music.SourceId);
            Check("Game-only session is hidden", SessionSelector.Select([game], settings, null) == null);
            Check("Video is hidden in music mode", SessionSelector.Select([video], settings, null) == null);
            Check("Unknown player with Music metadata is supported", SessionSelector.Select([Item("custom")], settings, null)?.SourceId == "custom");
            Check("Paused known music remains selected over playing game", SessionSelector.Select([game, Item("spotify", false, false, true)], settings, null)?.SourceId == "spotify");
            settings.PinnedSource = "browser.exe";
            Check("Explicit pin overrides music filter", SessionSelector.Select([music, video], settings, null)?.SourceId == video.SourceId);
            settings.PinnedSource = "closed-player";
            Check("Missing pin never falls back to game", SessionSelector.Select([music, game], settings, null) == null);
            settings.PinnedSource = ""; settings.ExcludedSources = ["SPOTIFY.EXE"];
            Check("Exclusions ignore case", SessionSelector.Select([music, game], settings, null) == null);
            settings.ExcludedSources = []; settings.MusicOnly = false;
            Check("Filter can be disabled", SessionSelector.Select([video], settings, null)?.SourceId == video.SourceId);
            settings.MusicOnly = true;
            Check("Stable source on equivalent sessions", SessionSelector.Select([Item("a"), Item("b")], settings, "b")?.SourceId == "b");
            Check("Playing music preferred to paused music", SessionSelector.Select([Item("a", false), Item("b")], settings, "a")?.SourceId == "b");
            var temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "test-profile");
            var store = new SettingsStore(temp); Directory.CreateDirectory(temp);
            RunShortcutChecks(Check, temp);
            settings.Scale = double.NaN; settings.HideSeconds = -1; settings.VolumeStep = 100; settings.Accent = "invalid";
            settings.Normalize();
            Check("Settings bounds are validated", settings.Scale == 1 && settings.HideSeconds == 1 && settings.VolumeStep == 10 && settings.Accent == "#88C9FF");
            settings.PinnedSource = "Spotify"; settings.Corner = Corner.TopRight;
            store.Save(settings); var loaded = store.Load();
            Check("Settings survive restart", loaded.PinnedSource == "Spotify" && loaded.Corner == Corner.TopRight);
            File.WriteAllText(store.FilePath, "{broken json");
            Check("Corrupted profile recovers", store.Load().HideSeconds == 4);
            var oldProfile = JsonSerializer.Deserialize<Settings>("{\"Scale\":1,\"Corner\":1}")!;
            Check("Older profile migrates to opaque background", oldProfile.BackgroundOpacity == 1 && oldProfile.BackgroundImagePath == "" && !oldProfile.ShowOnStartup);
            Check("Existing profiles keep the tray icon", oldProfile.ShowTrayIcon);
            Check("Software mixer changes never reveal overlay", !PresentationPolicy.ShouldReveal(PanelTrigger.AudioStateChanged, new Settings()));
            Check("Volume keys reveal overlay", PresentationPolicy.ShouldReveal(PanelTrigger.VolumeKey, new Settings()));
            Check("Hardware controls reveal overlay", PresentationPolicy.ShouldReveal(PanelTrigger.HardwareVolume, new Settings()));
            Check("Startup and media keys are quiet by default", !PresentationPolicy.ShouldReveal(PanelTrigger.Startup, new Settings()) && !PresentationPolicy.ShouldReveal(PanelTrigger.MediaKey, new Settings()));
            Check("Explicit preview is allowed", PresentationPolicy.ShouldReveal(PanelTrigger.ExplicitPreview, new Settings()));
            Check("Website labels can be disabled", MediaSourceLabel.For(Item("chrome") with { AppName = "Google Chrome", Website = "YouTube" }, new Settings { ShowWebsite = false }) == "Google Chrome");
            Check("Website labels include browser", MediaSourceLabel.For(Item("chrome") with { AppName = "Google Chrome", Website = "YouTube" }, new Settings()) == "YouTube · Google Chrome");
            Check("Known metadata host is recognized", MediaSourceLabel.FromMetadata("https://music.youtube.com/watch?v=example") == "YouTube");
            Check("Lookalike domains are not recognized", MediaSourceLabel.FromMetadata("https://youtube.com.example.org/watch") == null);
            Check("Unrelated browser window is ignored", MediaSourceLabel.FromMatchingWindow("My current track", "Other video - YouTube - Google Chrome") == null);
            Check("Matching browser window identifies site", MediaSourceLabel.FromMatchingWindow("My current track", "My current track - YouTube - Google Chrome") == "YouTube");
            using (var icon = Branding.CreateTrayIcon()) Check("Custom tray icon is embedded", icon.Width == 32 && icon.Height == 32);
            var openSignals = 0;
            using (var primary = new SingleInstance(temp, () => Interlocked.Increment(ref openSignals), () => { }))
            {
                using var secondary = new SingleInstance(temp, () => { }, () => { });
                await Task.Delay(100);
                Check("Only one background instance owns a profile", primary.IsPrimary && !secondary.IsPrimary && openSignals > 0);
            }
            var visualSettings = new Settings { BackgroundOpacity = double.NaN, ImageDarken = 999, MediaWidth = -1, PanelHeight = 999, Radius = -1 };
            visualSettings.Normalize();
            Check("Appearance bounds are safe", visualSettings.BackgroundOpacity == 1 && visualSettings.ImageDarken == .9 && visualSettings.MediaWidth == 300 && visualSettings.PanelHeight == 224 && visualSettings.Radius == 0);
            using (var keys = new KeyboardInterceptor()) { keys.VolumeEnabled = false; keys.MediaEnabled = false; }
            Check("Keyboard hook installs and releases", true);
            using var audio = new CoreAudioService(); audio.Refresh();
            checks.Add(new { name = "Audio endpoint read", available = audio.Available, volume = audio.Volume, muted = audio.Muted });
            using var media = new MediaSessionService(); await media.StartAsync();
            checks.Add(new { name = "Real media sessions", error = media.Error, sessions = media.Items.Select(s => new { s.SourceId, s.AppName, s.IsMusic, s.Playing, s.CanNext }).ToArray() });
            Check("Windows media API initializes", media.Error == null);
            var preferencesSettings = new Settings();
            var window = new SettingsWindow(preferencesSettings, temp);
            window.UpdateSources([], null, null, audio.Available);
            window.Show(); window.UpdateLayout();
            await Task.Delay(240);
            Check("Settings open with a support greeting", window.IsSupportGreetingVisible);
            var png = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "settings-preview.png");
            SaveVisual(window, png);
            Check("Settings window renders", File.Exists(png));
            await RunSupportChecksAsync(window, Path.GetDirectoryName(Path.GetFullPath(reportPath))!, Check);
            ((System.Windows.Controls.RadioButton)window.FindName("SupportNav")).IsChecked = true;
            await Task.Delay(240); window.UpdateLayout();
            SaveVisual(window, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "support-preview.png"));
            ((System.Windows.Controls.RadioButton)window.FindName("ControlNav")).IsChecked = true;
            var shortcutCheckbox = (System.Windows.Controls.CheckBox)window.FindName("ShortcutsEnabledBox");
            shortcutCheckbox.IsChecked = false;
            shortcutCheckbox.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Check("Settings checkbox disables shortcut editing and updates configuration", !preferencesSettings.ShortcutsEnabled && !((System.Windows.Controls.StackPanel)window.FindName("ShortcutRows")).IsEnabled);
            shortcutCheckbox.IsChecked = true;
            shortcutCheckbox.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            var editorRow = (ShortcutEditor)((System.Windows.Controls.StackPanel)window.FindName("ShortcutRows")).Children[0];
            editorRow.TryAssign(0x78, 6);
            Check("Editing a shortcut updates configuration and sidebar label", preferencesSettings.ShowPanelShortcut == new HotkeyBinding(0x78, 6) && ((System.Windows.Controls.TextBlock)window.FindName("ShortcutSummary")).Text == "Ctrl + Shift + F9");
            ((System.Windows.Controls.Button)window.FindName("ResetShortcuts")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Check("Reset restores all five default bindings", preferencesSettings.GetShortcuts().Count() == 5 && preferencesSettings.GetShortcuts().All(x => x.Binding == HotkeyBinding.Default(x.Command)));
            ((System.Windows.Controls.ScrollViewer)window.FindName("PageScroll")).ScrollToBottom();
            await Task.Delay(240); window.UpdateLayout();
            SaveVisual(window, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "shortcuts-preview.png"));
            window.Width = 900; window.Height = 650; window.UpdateLayout();
            ((System.Windows.Controls.ScrollViewer)window.FindName("PageScroll")).ScrollToBottom();
            await Task.Delay(240); window.UpdateLayout();
            SaveVisual(window, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "compact-settings-preview.png"));
            window.ShowSupportGreeting();
            await Task.Delay(240);
            Check("Greeting can appear on another explicit settings opening", window.IsSupportGreetingVisible);
            window.ActiveSupportDialog!.Close();
            await Task.Delay(80);
            Check("Greeting can be dismissed without donating", !window.IsSupportGreetingVisible);
            window.ShowSupportGreeting();
            await Task.Delay(10200);
            Check("Support greeting remains until a manual action", window.IsSupportGreetingVisible);
            window.ActiveSupportDialog!.Close();
            await Task.Delay(80);
            using (var hid = new HardwareVolumeListener(HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)!)) { }
            Check("Consumer HID input registers and releases", true);
            using var fakeAudio = new TestAudio();
            var overlaySettings = new Settings();
            var overlay = new OverlayWindow(fakeAudio, overlaySettings);
            overlay.Update(music);
            Check("State update does not show a hidden overlay", !overlay.IsVisible);
            overlay.Reveal(true); overlay.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            SaveVisual(overlay, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "overlay-preview.png"));
            Check("Overlay renders and does not activate", !overlay.IsActive && overlay.ActualWidth > 400);
            var initialPosition = overlay.PointToScreen(new Point());
            for (var n = 0; n < 80; n++) { fakeAudio.SetVolume(n % 100); overlay.Update(music); overlay.Reveal(true); }
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check("Repeated input does not recreate presentation", overlay.PresentationCount == 1);
            Check("Repeated interaction keeps full window opacity", overlay.Opacity == 1 && !DependencyPropertyHelper.GetValueSource(overlay, Window.OpacityProperty).IsAnimated);
            Check("Repeated interaction does not move the overlay", overlay.PointToScreen(new Point()) == initialPosition);
            var surface = new RenderTargetBitmap((int)overlay.ActualWidth, (int)overlay.ActualHeight, 96, 96, PixelFormats.Pbgra32); surface.Render(overlay);
            var pixel = new byte[4]; surface.CopyPixels(new Int32Rect(15, 70, 1, 1), pixel, 4, 0);
            Check("Default background pixels are opaque", pixel[3] == 255);
            overlay.Hide(); fakeAudio.SetVolume(45); overlay.Update(music);
            Check("Hidden overlay stays hidden on software state change", !overlay.IsVisible);
            overlaySettings.FadeOut = false; overlay.Reveal(true); overlay.BeginHide();
            Check("Fade toggle off hides immediately", !overlay.IsVisible && !overlay.IsFading);
            overlaySettings.FadeOut = true; overlaySettings.FadeMilliseconds = 600;
            overlay.Reveal(true); overlay.BeginHide();
            await Task.Delay(30);
            Check("Fade toggle on starts an opacity animation", overlay.IsVisible && overlay.IsFading && DependencyPropertyHelper.GetValueSource(overlay, Window.OpacityProperty).IsAnimated);
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (overlay.IsVisible && DateTime.UtcNow < deadline) await Task.Delay(40);
            Check("Fade completes and resets opacity while hidden", !overlay.IsVisible && !overlay.IsFading && overlay.Opacity == 1);
            overlay.Reveal(true); overlay.BeginHide(); await Task.Delay(30); overlay.Reveal(true); await Task.Delay(700);
            Check("New input cancels fade without stale completion hiding panel", overlay.IsVisible && !overlay.IsFading && overlay.Opacity == 1);
            overlaySettings.HideSeconds = 1; overlaySettings.FadeMilliseconds = 120; overlaySettings.HoldOnHover = false; overlay.ApplySettings(); overlay.Reveal(true);
            deadline = DateTime.UtcNow.AddSeconds(3);
            while (overlay.IsVisible && DateTime.UtcNow < deadline) await Task.Delay(40);
            Check("Configured timeout automatically fades and hides", !overlay.IsVisible);
            var bytes = new byte[600 * 400 * 4];
            for (var i = 0; i < bytes.Length; i += 4) { bytes[i] = 180; bytes[i + 1] = 90; bytes[i + 2] = 40; bytes[i + 3] = 255; }
            var image = BitmapSource.Create(600, 400, 96, 96, PixelFormats.Bgra32, null, bytes, 2400); image.Freeze();
            var crop = ImageTools.CropBounds(600, 400, 640, 220, 2, -9999, 9999);
            Check("Crop keeps the viewport covered", crop.Left <= 0 && crop.Top <= 0 && crop.Right >= 640 && crop.Bottom >= 220);
            var exported = ImageTools.ExportCrop(image, crop, 640, 220);
            Check("Crop export keeps panel aspect ratio", exported.PixelWidth == 1600 && exported.PixelHeight == 550);
            var imagePath = Path.Combine(temp, "image.png"); ImageTools.SavePng(exported, imagePath);
            Check("Imported background round-trips", ImageTools.Load(imagePath).PixelWidth == 1600);
            overlaySettings.BackgroundImagePath = imagePath; overlaySettings.BackgroundOpacity = .6; overlay.ApplySettings();
            overlay.Reveal(true); SaveVisual(overlay, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "custom-background-preview.png"));
            Check("Custom background does not fade text or entire window", overlay.Opacity == 1);
            var cropWindow = new CropWindow(image, (80 + overlaySettings.MediaWidth) / overlaySettings.PanelHeight); cropWindow.Show(); cropWindow.UpdateLayout();
            SaveVisual(cropWindow, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "crop-preview.png"));
            cropWindow.Close();
            Check("Crop editor renders", true);
            await RunLanguageChecksAsync(window, preferencesSettings, image, Path.GetDirectoryName(Path.GetFullPath(reportPath))!, Check);
            await RunInteractionChecksAsync(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, Check);
            overlay.Close(); window.Close();
            var lifecycleStore = new SettingsStore(Path.Combine(temp, "lifecycle"));
            var lifecycleSettings = new Settings { ShowTrayIcon = false };
            lifecycleStore.Save(lifecycleSettings);
            using (var controller = new MediaBack.Application.ApplicationController(lifecycleStore))
            {
                await controller.StartAsync(false);
                Check("Hidden tray preference survives restart and background startup", !lifecycleStore.Load().ShowTrayIcon && !controller.IsTrayVisible);
                Check("Background startup creates no settings window", controller.IsRunning && !controller.AreSettingsOpen);
                controller.ShowSettings();
                var firstWindow = System.Windows.Application.Current.MainWindow;
                Check("Settings open on demand", controller.AreSettingsOpen);
                var trayBox = (System.Windows.Controls.CheckBox)firstWindow.FindName("TrayIconBox");
                trayBox.IsChecked = true;
                trayBox.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Check("Preferences restore the tray icon immediately", controller.IsTrayVisible);
                trayBox.IsChecked = false;
                trayBox.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Check("Preferences hide the tray icon immediately", !controller.IsTrayVisible);
                controller.CloseSettings();
                Check("Closing settings leaves background service running", controller.IsRunning && !controller.AreSettingsOpen && System.Windows.Application.Current.MainWindow == null);
                controller.ShowSettings();
                Check("Reopening settings creates a fresh window", controller.AreSettingsOpen && !ReferenceEquals(firstWindow, System.Windows.Application.Current.MainWindow));
                controller.CloseSettings();
                Check("Closing preferences keeps hidden-tray service running", controller.IsRunning && !controller.IsTrayVisible);
                Check("Tray preference is saved when closing preferences", !lifecycleStore.Load().ShowTrayIcon);
            }
        }
        catch (Exception ex) { failed++; checks.Add(new { name = "Unhandled test exception", error = ex.ToString() }); }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { failed, checks }, new JsonSerializerOptions { WriteIndented = true }));
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }
    private sealed class TestAudio : IAudioService
    {
        public int Volume { get; private set; } = 52;
        public bool Muted { get; private set; }
        public bool Available => true;
        public int SetCalls { get; private set; }
        public event Action? Changed;
        public void Refresh() { }
        public void SetVolume(int volume) { SetCalls++; Volume = Math.Clamp(volume, 0, 100); Changed?.Invoke(); }
        public void Adjust(int delta) => SetVolume(Volume + delta);
        public void ToggleMute() { Muted = !Muted; Changed?.Invoke(); }
        public void Dispose() { }
    }
    private static void SaveVisual(Window window, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
