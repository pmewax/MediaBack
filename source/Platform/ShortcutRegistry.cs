using System.Runtime.InteropServices;
using MediaBack.Domain;

namespace MediaBack.Platform;



public sealed class ShortcutRegistry : IDisposable
{
    private readonly Func<int, uint, int, bool> register;
    private readonly Action<int> unregister;
    private (int Id, uint Modifiers, int Key)[] requested = [], registered = [];
    private (QuickCommand Command, string Key)[] issues = [];
    public string? Error => issues.Length == 0 ? null : string.Join("\n", issues.Select(x => L.Format(x.Key, QuickCommandNames.For(x.Command))));
    public int Count => registered.Length;
    public ShortcutRegistry(IntPtr window) : this((id, mods, key) => RegisterHotKey(window, id, mods | 0x4000, (uint)key), id => UnregisterHotKey(window, id)) { }
    internal ShortcutRegistry(Func<int, uint, int, bool> register, Action<int> unregister)
    { this.register = register; this.unregister = unregister; }
    public void Apply(Settings settings, bool suspended = false)
    {
        var next = settings.ShortcutsEnabled && !suspended
            ? settings.GetShortcuts().Where(x => x.Binding.Enabled && x.Binding.Key != 0)
                .Select(x => ((int)x.Command, x.Binding.Modifiers, x.Binding.Key)).ToArray() : [];
        if (requested.SequenceEqual(next)) return;
        Clear(); requested = next;
        var active = new List<(int Id, uint Modifiers, int Key)>();
        var errors = new List<(QuickCommand Command, string Key)>();
        foreach (var (id, modifiers, key) in next)
        {
            if (active.Any(x => x.Key == key && x.Modifiers == modifiers))
            { errors.Add(((QuickCommand)id, "ShortcutDuplicateAction")); continue; }
            if (register(id, modifiers, key)) active.Add((id, modifiers, key));
            else errors.Add(((QuickCommand)id, "ShortcutOccupiedAction"));
        }
        Volatile.Write(ref registered, active.ToArray());
        issues = errors.ToArray();
    }
    public bool Contains(int key, uint modifiers) => Volatile.Read(ref registered).Any(x => x.Key == key && x.Modifiers == modifiers);
    public bool TryResolve(int id, IntPtr messageData, out QuickCommand command)
    {
        var value = (ulong)messageData.ToInt64();
        var key = (int)((value >> 16) & 0xFFFF); var modifiers = (uint)value & 15;
        command = (QuickCommand)id;
        return registered.Any(x => x.Id == id && x.Key == key && x.Modifiers == modifiers);
    }
    private void Clear()
    {
        var old = registered; Volatile.Write(ref registered, []);
        foreach (var item in old) unregister(item.Id);
        issues = [];
    }
    public void Dispose() { Clear(); requested = []; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
