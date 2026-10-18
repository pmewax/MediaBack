using System.Windows.Interop;
using System.Windows.Threading;
using MediaBack.Domain;

namespace MediaBack.Platform;


public sealed class InputService : IDisposable
{
    private readonly HwndSource source;
    private readonly ShortcutRegistry shortcuts;
    private KeyboardInterceptor? keyboard;
    private HardwareVolumeListener? hardware;
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer hardwareTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private bool disposed, recording, volumeEnabled, mediaEnabled, hidEnabled;
    private Exception? keyboardError, hardwareError;
    private Settings? settings;
    public bool HardwareEnabled { get; private set; }
    public string? Error => string.Join("\n", new[] { shortcuts.Error, keyboardError == null ? null : L.Get("KeysError") + keyboardError.Message,
        hardwareError == null ? null : L.Get("HidError") + hardwareError.Message }.Where(x => !string.IsNullOrEmpty(x))) is { Length: > 0 } text ? text : null;
    public string? ShortcutError => shortcuts.Error;
    internal int RegisteredShortcutCount => shortcuts.Count;
    public event Action<int>? KeyPressed;
    public event Action? HardwareVolume, ShowPanel, GameInteraction;
    public InputService()
    {
        source = new HwndSource(new HwndSourceParameters("MediaBack.Input") { ParentWindow = (IntPtr)(-3), WindowStyle = 0, Width = 0, Height = 0 });
        shortcuts = new ShortcutRegistry(source.Handle);
        source.AddHook(Message);
        hardwareTimer.Tick += (_, _) => { hardwareTimer.Stop(); if (HardwareEnabled && !disposed) HardwareVolume?.Invoke(); };
        try { hardware = new HardwareVolumeListener(source); hardware.VolumeInput += () => { if (HardwareEnabled && !hardwareTimer.IsEnabled) hardwareTimer.Start(); }; }
        catch (Exception ex) { hardwareError = ex; }
    }
    public void ConfigureShortcuts(Settings value)
    { if (disposed) return; settings = value; shortcuts.Apply(value, recording); }
    public void SetCapture(bool value)
    {
        if (disposed || recording == value) return;
        recording = value;
        if (settings != null) shortcuts.Apply(settings, recording);
        ApplyInputState();
    }
    public void SetEnabled(bool volume, bool media, bool hid)
    {
        if (disposed) return;
        volumeEnabled = volume; mediaEnabled = media; hidEnabled = hid;
        ApplyInputState();
    }
    private void ApplyInputState()
    {
        HardwareEnabled = hidEnabled && !recording;
        if (!HardwareEnabled) hardwareTimer.Stop();
        var needsHook = !recording && (volumeEnabled || mediaEnabled);
        if (needsHook && keyboard == null && keyboardError == null)
        {
            try
            {
                keyboard = new KeyboardInterceptor { IsShortcut = shortcuts.Contains };
                keyboard.KeyPressed += key => dispatcher.BeginInvoke(() => { if (!disposed && !recording) KeyPressed?.Invoke(key); });
            }
            catch (Exception ex) { keyboardError = ex; }
        }
        if (!needsHook) { keyboard?.Dispose(); keyboard = null; }
        if (keyboard != null) { keyboard.VolumeEnabled = volumeEnabled; keyboard.MediaEnabled = mediaEnabled; }
    }
    private IntPtr Message(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != 0x312 || disposed) return IntPtr.Zero;
        handled = true;
        if (recording || !shortcuts.TryResolve(wParam.ToInt32(), lParam, out var command)) return IntPtr.Zero;
        switch (command)
        {
            case QuickCommand.ShowPanel: ShowPanel?.Invoke(); break;
            case QuickCommand.GameInteraction: GameInteraction?.Invoke(); break;
            case QuickCommand.Previous: KeyPressed?.Invoke(0xB1); break;
            case QuickCommand.Next: KeyPressed?.Invoke(0xB0); break;
            case QuickCommand.PlayPause: KeyPressed?.Invoke(0xB3); break;
        }
        return IntPtr.Zero;
    }
    public void Dispose()
    {
        disposed = true; hardwareTimer.Stop(); keyboard?.Dispose(); hardware?.Dispose(); shortcuts.Dispose();
        source.RemoveHook(Message); source.Dispose();
    }
}
