using System.IO;
using System.Text.Json;
using System.Windows.Interop;
using MediaBack.Domain;
using MediaBack.Platform;
using MediaBack.UI;

namespace MediaBack;

internal static partial class Diagnostics
{
    private static void RunShortcutChecks(Action<string, bool> check, string directory)
    {
        var old = JsonSerializer.Deserialize<Settings>("{}")!;
        check("Older profiles retain default shortcut bindings", old.ShortcutsEnabled && old.ShowPanelShortcut == new HotkeyBinding(0x56) && old.NextShortcut == new HotkeyBinding(0x27));
        var store = new SettingsStore(Path.Combine(directory, "custom-shortcuts"));
        var config = new Settings { ShortcutsEnabled = false, ShowPanelShortcut = new(0x78, 6), PreviousShortcut = new(0x77, 8, false) };
        store.Save(config); var loaded = store.Load();
        check("Custom bindings and individual/global toggles persist", !loaded.ShortcutsEnabled && loaded.ShowPanelShortcut == config.ShowPanelShortcut && loaded.PreviousShortcut == config.PreviousShortcut);
        check("Modifier-only keys cannot be assigned", !HotkeyBinding.IsAssignableKey(0x11) && !HotkeyBinding.IsAssignableKey(0x5B));
        check("Function, ordinary and media keys are assignable", HotkeyBinding.IsAssignableKey(0x87) && HotkeyBinding.IsAssignableKey(0x41) && HotkeyBinding.IsAssignableKey(0xB3));
        check("Invalid persisted shortcut recovers safely", HotkeyBinding.Normalize(new(999, 99), QuickCommand.Next) == HotkeyBinding.Default(QuickCommand.Next));
        check("Extra modifier bits are stripped", HotkeyBinding.Normalize(new(0x78, 255), QuickCommand.Next).Modifiers == 15);
        var calls = 0; var releases = 0;
        using (var registry = new ShortcutRegistry((id, modifiers, key) => { calls++; return key != 0x79; }, _ => releases++))
        {
            config = new Settings { GameInteractionShortcut = new(0) }; registry.Apply(config); registry.Apply(config);
            check("Unchanged shortcuts are not registered twice", calls == 4 && registry.Count == 4);
            var data = (IntPtr)((0x56 << 16) | 3);
            check("A registered message resolves its command", registry.TryResolve(1, data, out var command) && command == QuickCommand.ShowPanel);
            config.ShowPanelShortcut = new(0x78, 6); registry.Apply(config);
            check("Old messages are rejected after rebinding", !registry.TryResolve(1, data, out _) && registry.TryResolve(1, (IntPtr)((0x78 << 16) | 6), out _));
            config.ShortcutsEnabled = false; registry.Apply(config);
            check("Global off unregisters every shortcut", registry.Count == 0 && releases == 8 && !registry.TryResolve(1, data, out _));
            config.ShortcutsEnabled = true; config.NextShortcut = config.PreviousShortcut; registry.Apply(config);
            check("Duplicate commands are reported without double registration", registry.Count == 3 && registry.Error?.Contains("уже используется") == true);
            config.NextShortcut = config.NextShortcut with { Enabled = false }; registry.Apply(config);
            check("Individual off releases a binding and clears its conflict", registry.Count == 3 && registry.Error == null);
            config.NextShortcut = new(0x79, 7); registry.Apply(config);
            check("Reserved or externally occupied binding reports its action", registry.Count == 3 && registry.Error?.Contains("Следующий трек") == true);
            registry.Apply(config, true);
            check("Capture suspends all global shortcuts", registry.Count == 0 && registry.Error == null);
            registry.Apply(config);
            check("Capture completion restores available shortcuts", registry.Count == 3);
        }
        var editor = new ShortcutEditor(QuickCommand.ShowPanel, new(0x56)); var changes = 0;
        editor.Changed += () => changes++;
        editor.BeginRecording(); editor.CancelRecording();
        check("Cancel recording keeps the original binding", editor.Binding == new HotkeyBinding(0x56) && changes == 0 && !editor.IsRecording);
        check("Recorder accepts a custom function-key binding", editor.TryAssign(0x78, 6) && editor.Binding == new HotkeyBinding(0x78, 6) && changes == 1);
        editor.Validate = _ => "Duplicate";
        check("Recorder rejects conflicts without replacing the working binding", !editor.TryAssign(0x77, 3) && editor.Binding == new HotkeyBinding(0x78, 6) && changes == 1);
        check("Shortcut display matches modifiers and key", ShortcutText.Format(new(0x78, 6)) == "Ctrl + Shift + F9" && ShortcutText.Format(new(0, 0)) == "Не назначено");
        var nativeConfig = new Settings { ShowPanelShortcut = new(0x87, 7), PreviousShortcut = new(0), NextShortcut = new(0), PlayPauseShortcut = new(0), GameInteractionShortcut = new(0) };
        using (var window1 = new HwndSource(new HwndSourceParameters("MediaBack.ShortcutTest1") { ParentWindow = (IntPtr)(-3) }))
        using (var window2 = new HwndSource(new HwndSourceParameters("MediaBack.ShortcutTest2") { ParentWindow = (IntPtr)(-3) }))
        using (var first = new ShortcutRegistry(window1.Handle))
        using (var second = new ShortcutRegistry(window2.Handle))
        {
            first.Apply(nativeConfig); second.Apply(nativeConfig);
            check("Windows registers a custom key and rejects a real conflict", first.Count == 1 && second.Count == 0 && second.Error != null);
            first.Apply(nativeConfig, true); second.Apply(nativeConfig, true); second.Apply(nativeConfig);
            check("Windows binding is reusable after unregister", second.Count == 1 && second.Error == null);
        }
        using (var input = new InputService())
        {
            input.ConfigureShortcuts(nativeConfig);
            check("Input service applies a custom binding", input.RegisteredShortcutCount == 1);
            input.SetCapture(true);
            check("Input service releases bindings while recording", input.RegisteredShortcutCount == 0);
            input.SetCapture(false);
            check("Input service restores bindings after recording", input.RegisteredShortcutCount == 1);
            nativeConfig.ShortcutsEnabled = false; input.ConfigureShortcuts(nativeConfig);
            check("Input service honors the global shortcut checkbox", input.RegisteredShortcutCount == 0);
            input.SetEnabled(false, false, true);
            check("Disabling quick commands does not disable hardware volume input", input.HardwareEnabled);
            input.SetCapture(true);
            check("Recording suspends hardware presentation too", !input.HardwareEnabled);
            input.SetCapture(false);
            check("Hardware presentation resumes after recording", input.HardwareEnabled);
        }
        check("Donation catalog preserves Tron addresses exactly", SupportCatalog.Wallets.Where(x => x.Network.StartsWith("Tron")).Count() == 2 && SupportCatalog.Wallets.Where(x => x.Network.StartsWith("Tron")).All(x => x.Address == "TURJryLPSm91EeDJoAHN6bpGPrkLTJDAoc"));
        check("Donation catalog preserves Solana address exactly", SupportCatalog.Wallets.Single(x => x.Network.StartsWith("Solana")).Address == "3pt1fwASJfbQtowHVzgB9bxEUrEpBFoN9SnqXuR99D2K");
        check("Donation catalog labels all three EVM receiving networks", SupportCatalog.Wallets.Count == 6 && SupportCatalog.Wallets.Count(x => x.Address == "0xC74F8B058e937007a40978808E073418dA1132fd") == 3);
    }
}
