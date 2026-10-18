using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MediaBack.Domain;

namespace MediaBack.UI;

public sealed class ShortcutEditor : Grid
{
    private readonly CheckBox enabled;
    private readonly Button recorder;
    private readonly TextBlock error;
    private HotkeyBinding binding;
    public QuickCommand Command { get; }
    public HotkeyBinding Binding => binding;
    public bool IsRecording { get; private set; }
    public Func<HotkeyBinding, string?>? Validate { get; set; }
    public event Action? Changed;
    public event Action<bool>? RecordingChanged;
    public ShortcutEditor(QuickCommand command, HotkeyBinding binding)
    {
        Command = command; this.binding = binding;
        Margin = new Thickness(0, 0, 0, 12);
        ColumnDefinitions.Add(new ColumnDefinition());
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        enabled = new CheckBox { Content = QuickCommandNames.For(command), IsChecked = binding.Enabled, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        enabled.Click += (_, _) => { this.binding = this.binding with { Enabled = enabled.IsChecked == true }; Changed?.Invoke(); };
        Children.Add(enabled);
        recorder = new Button { MinHeight = 40, Padding = new Thickness(10, 8, 10, 8), FontSize = 12 };
        SetColumn(recorder, 1); Children.Add(recorder);
        AutomationProperties.SetName(recorder, L.Get("ShortcutPrefix") + QuickCommandNames.For(command));
        recorder.Click += (_, _) => { if (IsRecording) CancelRecording(); else BeginRecording(); };
        recorder.PreviewKeyDown += CaptureKey;
        recorder.LostKeyboardFocus += (_, _) => CancelRecording();
        var clear = new Button { Content = "×", FontSize = 18, Padding = new Thickness(0), Margin = new Thickness(6, 0, 0, 0), MinHeight = 40 };
        AutomationProperties.SetName(clear, L.Get("ClearShortcut"));
        clear.Click += (_, _) => { CancelRecording(); this.binding = this.binding with { Key = 0 }; Refresh(); Changed?.Invoke(); };
        SetColumn(clear, 2); Children.Add(clear);
        error = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(244, 167, 177)), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
        SetRow(error, 1); SetColumnSpan(error, 3); Children.Add(error);
        Refresh();
    }
    public void BeginRecording()
    {
        if (IsRecording) return;
        IsRecording = true; error.Visibility = Visibility.Collapsed;
        RecordingChanged?.Invoke(true); recorder.Content = L.Get("PressKeys");
        recorder.Focus();
    }
    private void CaptureKey(object sender, KeyEventArgs e)
    {
        if (!IsRecording) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None) { CancelRecording(); return; }
        TryAssign(KeyInterop.VirtualKeyFromKey(key), (uint)Keyboard.Modifiers);
    }
    internal bool TryAssign(int key, uint modifiers)
    {
        if (!HotkeyBinding.IsAssignableKey(key)) return false;
        var next = binding with { Key = key, Modifiers = modifiers & 15 };
        var message = Validate?.Invoke(next);
        if (message != null) { error.Text = message; error.Visibility = Visibility.Visible; return false; }
        binding = next; Refresh(); Changed?.Invoke(); CancelRecording(); return true;
    }
    public void CancelRecording()
    {
        if (!IsRecording) return;
        IsRecording = false; Refresh(); RecordingChanged?.Invoke(false);
    }
    private void Refresh() => recorder.Content = ShortcutText.Format(binding);
}

public static class ShortcutText
{
    public static string Format(HotkeyBinding binding)
    {
        if (binding.Key == 0) return L.Get("Unassigned");
        var parts = new List<string>();
        if ((binding.Modifiers & 2) != 0) parts.Add("Ctrl");
        if ((binding.Modifiers & 1) != 0) parts.Add("Alt");
        if ((binding.Modifiers & 4) != 0) parts.Add("Shift");
        if ((binding.Modifiers & 8) != 0) parts.Add("Win");
        parts.Add(binding.Key switch
        {
            0x20 => "Space", 0x25 => "←", 0x26 => "↑", 0x27 => "→", 0x28 => "↓",
            0x0D => "Enter", 0x1B => "Esc", 0x08 => "Backspace", 0x2E => "Delete",
            0x09 => "Tab", 0xBA => ";", 0xBB => "+", 0xBC => ",", 0xBD => "−", 0xBE => ".", 0xBF => "/",
            >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)binding.Key).ToString(),
            >= 0x70 and <= 0x87 => "F" + (binding.Key - 0x6F),
            >= 0x60 and <= 0x69 => "Num " + (binding.Key - 0x60),
            _ => KeyInterop.KeyFromVirtualKey(binding.Key).ToString()
        });
        return string.Join(" + ", parts);
    }
}
