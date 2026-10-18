using System.Windows;
using System.Windows.Threading;
using MediaBack.Domain;
using MediaBack.Platform;
using MediaBack.UI;

namespace MediaBack.Application;

public sealed class ApplicationController : IDisposable
{
    private readonly SettingsStore store;
    private readonly Settings settings;
    private readonly IAudioService audio;
    private readonly IMediaService media;
    private readonly OverlayWindow overlay;
    private readonly InputService input;
    private readonly TrayService tray;
    private SettingsWindow? preferences;
    private MediaItem? selected;
    private bool disposed, commandBusy;
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly DispatcherTimer healthTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    public event Action? ExitRequested;
    public bool AreSettingsOpen => preferences != null;
    public bool IsRunning => !disposed;
    internal bool IsTrayVisible => tray.IsVisible;
    public void CloseSettings() => preferences?.Close();
    public ApplicationController(SettingsStore store)
    {
        this.store = store; settings = store.Load();
        L.SetLanguage(settings.Language);
        audio = new CoreAudioService(); media = new MediaSessionService();
        overlay = new OverlayWindow(audio, settings); input = new InputService(); tray = new TrayService(settings.ShowTrayIcon);
        input.ConfigureShortcuts(settings);
        audio.Changed += RefreshAudio; media.Changed += RefreshState;
        overlay.CommandRequested += async command => await SendCommandAsync(command);
        input.KeyPressed += HandleKey; input.ShowPanel += () => overlay.Reveal();
        input.GameInteraction += () => overlay.ToggleInteraction();
        input.HardwareVolume += () => { audio.Refresh(); overlay.Update(selected); overlay.Reveal(); };
        tray.SettingsRequested += ShowSettings; tray.PanelRequested += () => overlay.Reveal(); tray.ExitRequested += () => ExitRequested?.Invoke();
        saveTimer.Tick += (_, _) => FlushSettings();
        healthTimer.Tick += (_, _) => audio.Refresh(); healthTimer.Start();
        RefreshState();
    }
    public async Task StartAsync(bool showSettings)
    {
        if (showSettings) ShowSettings();
        if (settings.ShowOnStartup) overlay.Reveal(true);
        await media.StartAsync();
    }
    public void ShowSettings()
    {
        if (disposed) return;
        if (preferences == null)
        {
            var window = new SettingsWindow(settings, store.DirectoryPath);
            preferences = window;
            window.SettingsChanged += ApplySettings;
            window.ShortcutCaptureChanged += active => { input.SetCapture(active); window.UpdateShortcutStatus(input.ShortcutError); };
            window.PreviewRequested += () => { overlay.Update(selected); overlay.Reveal(selected == null); };
            window.ExitRequested += CloseSettings;
            window.ShutdownRequested += () => ExitRequested?.Invoke();
            window.TrayRequested += CloseSettings;
            window.StartupChanged += enabled => { try { StartupRegistration.Set(enabled); } catch (Exception ex) { window.SetNotice(L.Get("AutostartError") + ex.Message); } };
            window.Closed += (_, _) =>
            {
                FlushSettings();
                if (!disposed) input.SetCapture(false);
                if (ReferenceEquals(preferences, window)) preferences = null;
                if (ReferenceEquals(System.Windows.Application.Current.MainWindow, window)) System.Windows.Application.Current.MainWindow = null!;
            };
            if (input.Error != null) window.SetNotice(input.Error);
            System.Windows.Application.Current.MainWindow = window;
        }
        preferences.Show(); preferences.WindowState = WindowState.Normal; preferences.Activate(); RefreshState();
        preferences.ShowSupportGreeting();
    }
    private void RefreshAudio()
    {
        if (disposed) return;
        input.SetEnabled(settings.InterceptVolume && audio.Available, settings.InterceptMedia && selected != null, settings.HardwareControls);
        overlay.Update(selected);
        preferences?.UpdateAudioAvailability(audio.Available);
    }
    private void RefreshState()
    {
        if (disposed) return;
        selected = SessionSelector.Select(media.Items, settings, selected?.SourceId);
        input.SetEnabled(settings.InterceptVolume && audio.Available, settings.InterceptMedia && selected != null, settings.HardwareControls);
        overlay.Update(selected);
        preferences?.UpdateSources(media.Items, selected, media.Error, audio.Available);
        preferences?.UpdateShortcutStatus(input.ShortcutError);
    }
    private void ApplySettings() { saveTimer.Stop(); saveTimer.Start(); input.ConfigureShortcuts(settings); tray.SetVisible(settings.ShowTrayIcon); RefreshState(); overlay.ApplySettings(); }
    private void FlushSettings()
    {
        if (!saveTimer.IsEnabled) return;
        saveTimer.Stop();
        try { store.Save(settings); } catch (Exception ex) { preferences?.SetNotice(L.Get("SaveError") + ex.Message); }
    }
    private async void HandleKey(int key)
    {
        if (disposed) return;
        switch (key)
        {
            case 0xAD: audio.ToggleMute(); break;
            case 0xAE: audio.Adjust(-settings.VolumeStep); break;
            case 0xAF: audio.Adjust(settings.VolumeStep); break;
            case 0xB0: await SendCommandAsync(MediaCommand.Next); break;
            case 0xB1: await SendCommandAsync(MediaCommand.Previous); break;
            case 0xB3: await SendCommandAsync(MediaCommand.TogglePlayPause); break;
        }
        if (disposed) return;
        overlay.Update(selected);
        if (PresentationPolicy.ShouldReveal(key is >= 0xAD and <= 0xAF ? PanelTrigger.VolumeKey : PanelTrigger.MediaKey, settings)) overlay.Reveal();
    }
    private async Task SendCommandAsync(MediaCommand command)
    {
        if (disposed || commandBusy || selected == null) return;
        if (!(command switch { MediaCommand.Next => selected.CanNext, MediaCommand.Previous => selected.CanPrevious, _ => selected.CanPlayPause }))
        { preferences?.SetNotice(L.Get("PlayerNoCommand")); return; }
        commandBusy = true;
        try { if (!await media.CommandAsync(selected.SourceId, command)) preferences?.SetNotice(L.Get("PlayerCommandFailed")); }
        finally { commandBusy = false; }
        if (!disposed) RefreshState();
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; FlushSettings(); healthTimer.Stop();
        preferences?.CancelShortcutCapture();
        input.Dispose(); tray.Dispose(); audio.Changed -= RefreshAudio; media.Changed -= RefreshState;
        media.Dispose(); audio.Dispose(); overlay.Close(); preferences?.Close(); preferences = null;
    }
}
