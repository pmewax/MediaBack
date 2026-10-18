using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MediaBack.Domain;
using MediaBack.Platform;
using MediaBack.UI;

namespace MediaBack;

internal static partial class Diagnostics
{
    internal static async Task RunInteractionOnlyAsync(string? output)
    {
        L.SetLanguage(AppLanguage.Russian);
        output ??= Path.Combine(AppContext.BaseDirectory, "interaction-checks.json");
        var directory = Path.GetDirectoryName(Path.GetFullPath(output))!; Directory.CreateDirectory(directory);
        var checks = new List<object>(); var failed = 0;
        try { await RunInteractionChecksAsync(directory, (name, passed) => { checks.Add(new { name, passed }); if (!passed) failed++; }); }
        catch (Exception ex) { failed++; checks.Add(new { name = "Unhandled test exception", error = ex.ToString() }); }
        File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(new { failed, checks }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }
    private static async Task RunInteractionChecksAsync(string output, Action<string, bool> check)
    {
        var counter = -3; var shows = 0; var hides = 0;
        using (var lease = new CursorVisibilityLease(show => { if (show) { shows++; return ++counter; } hides++; return --counter; }))
        {
            lease.Acquire(); lease.Acquire();
            check("Cursor lease reveals a hidden cursor once without unbounded increments", counter == 0 && shows == 3);
            lease.Dispose(); lease.Dispose();
            check("Cursor lease restores the exact original count and is idempotent", counter == -3 && hides == 3);
        }
        using (var lease = new CursorVisibilityLease(show => show ? ++counter : --counter))
        {
            counter = -100; lease.Acquire(); check("Cursor recovery is bounded even for a corrupt counter", lease.IncrementCount == 16); lease.Dispose();
            check("Bounded cursor recovery still balances all changes", counter == -100);
        }
        using var audio = new TestAudio(); audio.SetVolume(10);
        var config = new Settings { HideSeconds = 20, HoldOnHover = true };
        var overlay = new OverlayWindow(audio, config); overlay.Reveal(); overlay.UpdateLayout();
        var slider = (Slider)overlay.FindName("VolumeSlider");
        var firstFrame = new TaskCompletionSource<bool>();
        EventHandler? onFrame = null;
        onFrame = (_, _) =>
        {
            CompositionTarget.Rendering -= onFrame;
            var visualTrack = (Track)slider.Template.FindName("PART_Track", slider);
            firstFrame.TrySetResult(DependencyPropertyHelper.GetValueSource(visualTrack, Track.ValueProperty).IsAnimated
                && !DependencyPropertyHelper.GetValueSource(slider, Slider.ValueProperty).IsAnimated);
        };
        if (SystemParameters.ClientAreaAnimation) CompositionTarget.Rendering += onFrame;
        audio.SetVolume(20); overlay.Update(null);
        check("Actual volume and slider target update immediately", audio.Volume == 20 && slider.Value == 20);
        if (SystemParameters.ClientAreaAnimation)
        {
            var frameResult = false;
            try { frameResult = await firstFrame.Task.WaitAsync(TimeSpan.FromSeconds(2)); }
            finally { CompositionTarget.Rendering -= onFrame; }
            check("Rendered volume transition animates only the indicator, not the real slider target", frameResult);
            await Task.Delay(160);
            check("Completed visual transition reaches the exact target", Math.Abs(overlay.DisplayedVolume - 20) < .01);
        }
        for (var i = 30; i <= 90; i += 10) { audio.SetVolume(i); overlay.Update(null); }
        var writes = audio.SetCalls;
        await Task.Delay(160);
        check("Rapid retargeting finishes at the latest value without modifying audio", Math.Abs(overlay.DisplayedVolume - 90) < .01 && audio.Volume == 90 && audio.SetCalls == writes);
        audio.SetVolume(10); overlay.Update(null);
        slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
        slider.Value = 37; await Task.Delay(130);
        check("Direct slider input cancels smoothing and has no delayed audio writes", audio.Volume == 37 && slider.Value == 37 && Math.Abs(overlay.DisplayedVolume - 37) < .01);
        config.SmoothVolume = false; overlay.ApplySettings(); audio.SetVolume(65); overlay.Update(null);
        check("Smoothing off snaps immediately", overlay.DisplayedVolume == 65);
        overlay.Hide(); config.SmoothVolume = true; audio.SetVolume(40); overlay.Update(null);
        var track = (Track)slider.Template.FindName("PART_Track", slider);
        check("Hidden updates do not animate or reveal the flyout", !overlay.IsVisible && overlay.DisplayedVolume == 40 && !DependencyPropertyHelper.GetValueSource(track, Track.ValueProperty).IsAnimated);
        overlay.Update(new MediaItem("test-player", "Test player", "Test track", "Test artist", true, true, true, true, true, true, TimeSpan.Zero, TimeSpan.FromMinutes(3)));
        var nextCommands = 0;
        overlay.CommandRequested += command => { if (command == MediaCommand.Next) nextCommands++; };
        var game = new Window { Title = "MediaBack cursor test", Width = 420, Height = 220, Cursor = Cursors.None, Content = new TextBlock { Text = "Diagnostic game window" } };
        game.Show(); game.Activate(); await Task.Delay(120);
        var gameHwnd = new WindowInteropHelper(game).Handle;
        overlay.Reveal(); var hwnd = new WindowInteropHelper(overlay).Handle;
        check("Normal flyout retains no-activate style", (GetWindowLongForTest(hwnd, -20).ToInt64() & 0x08000000L) != 0);
        var foreground = ForegroundForTest();
        var result = SendMessageForTest(hwnd, 0x20, hwnd, (IntPtr)1);
        check("Client hover handles a hidden cursor without activating the flyout", result == (IntPtr)1 && ForegroundForTest() == foreground);
        overlay.ToggleInteraction(); await Task.Delay(120);
        check("Explicit game hotkey permits interactive foreground focus", overlay.IsInteracting && ForegroundForTest() == hwnd && (GetWindowLongForTest(hwnd, -20).ToInt64() & 0x08000000L) == 0);
        check("Game interaction owns pointer input even outside the panel", overlay.IsMouseCaptureWithin);
        Click(overlay, "NextButton");
        check("Track controls work in game interaction without dismissing the panel", nextCommands == 1 && overlay.IsInteracting && ForegroundForTest() == hwnd);
        SaveVisual(overlay, Path.Combine(output, "game-interaction.png"));
        overlay.RaiseEvent(new System.Windows.Input.KeyEventArgs(Keyboard.PrimaryDevice, HwndSource.FromHwnd(hwnd), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        await Task.Delay(260);
        check("Escape releases interaction and restores the previous game", !overlay.IsInteracting && ForegroundForTest() == gameHwnd && (GetWindowLongForTest(hwnd, -20).ToInt64() & 0x08000000L) != 0);
        check("Escape releases all panel pointer capture", !overlay.IsMouseCaptureWithin);
        check("Focus restoration never targets another user-selected window", !OverlayWindow.CanRestoreForeground((IntPtr)3, (IntPtr)1, (IntPtr)2) && OverlayWindow.CanRestoreForeground((IntPtr)1, (IntPtr)1, (IntPtr)2));
        overlay.ToggleInteraction(); await Task.Delay(80); game.Activate(); await Task.Delay(80);
        check("Clicking away leaves game focus intact and releases interaction", !overlay.IsInteracting && ForegroundForTest() == gameHwnd);
        overlay.Close(); game.Close();
    }
    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")] private static extern IntPtr ForegroundForTest();
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongForTest(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern IntPtr SendMessageForTest(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
