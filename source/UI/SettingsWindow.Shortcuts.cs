using System.Windows;
using MediaBack.Domain;

namespace MediaBack.UI;

public partial class SettingsWindow
{
    private readonly List<ShortcutEditor> shortcutEditors = new();
    public event Action<bool>? ShortcutCaptureChanged;
    private void FillShortcuts()
    {
        CancelShortcutCapture(); shortcutEditors.Clear(); ShortcutRows.Children.Clear();
        ShortcutsEnabledBox.IsChecked = settings.ShortcutsEnabled;
        foreach (var (command, binding) in settings.GetShortcuts())
        {
            var editor = new ShortcutEditor(command, binding);
            editor.Validate = candidate => candidate.Enabled && settings.GetShortcuts().Any(x => x.Command != command && x.Binding.Enabled && x.Binding.Key == candidate.Key && x.Binding.Modifiers == candidate.Modifiers)
                ? L.Get("DuplicateShortcut") : null;
            editor.Changed += () => { settings.SetShortcut(command, editor.Binding); RefreshShortcutSummary(); SettingsChanged?.Invoke(); };
            editor.RecordingChanged += active =>
            {
                if (active) foreach (var other in shortcutEditors.Where(x => !ReferenceEquals(x, editor))) other.CancelRecording();
                ShortcutCaptureChanged?.Invoke(shortcutEditors.Any(x => x.IsRecording));
            };
            shortcutEditors.Add(editor); ShortcutRows.Children.Add(editor);
        }
        RefreshShortcutSummary();
    }
    private void RefreshShortcutSummary()
    {
        ShortcutRows.IsEnabled = settings.ShortcutsEnabled; ShortcutRows.Opacity = settings.ShortcutsEnabled ? 1 : .45;
        ShortcutSummary.Text = settings.ShortcutsEnabled && settings.ShowPanelShortcut.Enabled && settings.ShowPanelShortcut.Key != 0
            ? ShortcutText.Format(settings.ShowPanelShortcut) : L.Get("QuickOff");
    }
    private void ShortcutsEnabledClicked(object sender, RoutedEventArgs e)
    {
        CancelShortcutCapture(); settings.ShortcutsEnabled = ShortcutsEnabledBox.IsChecked == true;
        RefreshShortcutSummary(); SettingsChanged?.Invoke();
    }
    private void ResetShortcutsClicked(object sender, RoutedEventArgs e)
    {
        CancelShortcutCapture();
        foreach (var command in Enum.GetValues<QuickCommand>()) settings.SetShortcut(command, HotkeyBinding.Default(command));
        FillShortcuts(); SettingsChanged?.Invoke();
    }
    public void UpdateShortcutStatus(string? message)
    { ShortcutStatus.Text = message ?? L.Get("ShortcutHint"); }
    public void CancelShortcutCapture() { foreach (var editor in shortcutEditors) editor.CancelRecording(); }
}
