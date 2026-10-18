namespace MediaBack.Domain;

public enum QuickCommand { ShowPanel = 1, Previous = 2, Next = 3, PlayPause = 4, GameInteraction = 5 }

public sealed record HotkeyBinding(int Key, uint Modifiers = 3, bool Enabled = true)
{
    public static HotkeyBinding Default(QuickCommand command) => new(command switch
    {
        QuickCommand.ShowPanel => 0x56, QuickCommand.Previous => 0x25,
        QuickCommand.Next => 0x27, QuickCommand.GameInteraction => 0x4D, _ => 0x20
    });
    public static bool IsAssignableKey(int key) => key is > 0 and <= 254 && key is not
        (0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or 0xE5);
    public static HotkeyBinding Normalize(HotkeyBinding? binding, QuickCommand command) => binding == null ? Default(command)
        : binding.Key == 0 || IsAssignableKey(binding.Key) ? binding with { Modifiers = binding.Modifiers & 15 }
        : Default(command);
}

public static class QuickCommandNames
{
    public static string For(QuickCommand command) => command switch
    {
        QuickCommand.ShowPanel => L.Get("ShowFlyout"), QuickCommand.Previous => L.Get("PreviousTrack"),
        QuickCommand.Next => L.Get("NextTrack"), QuickCommand.GameInteraction => L.Get("GameInteraction"), _ => L.Get("PlayPauseAction")
    };
}
