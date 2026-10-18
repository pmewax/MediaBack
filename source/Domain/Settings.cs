using System.IO;
using System.Text.Json;

namespace MediaBack.Domain;

public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight, Custom }
public enum MonitorChoice { Primary, Cursor }
public enum PanelTheme { Graphite, Midnight, Warm, Classic }

public sealed class Settings
{
    public AppLanguage Language { get; set; } = AppLanguage.Russian;
    public bool ShowCursorOnHover { get; set; } = true;
    public bool SmoothVolume { get; set; } = true;
    public Corner Corner { get; set; } = Corner.TopLeft;
    public MonitorChoice Monitor { get; set; } = MonitorChoice.Primary;
    public double OffsetX { get; set; } = 28;
    public double OffsetY { get; set; } = 44;
    public double Scale { get; set; } = 1;
    public int HideSeconds { get; set; } = 4;
    public int VolumeStep { get; set; } = 2;
    public bool InterceptVolume { get; set; } = true;
    public bool InterceptMedia { get; set; } = true;
    public bool MusicOnly { get; set; } = true;
    public string PinnedSource { get; set; } = "";
    public string[] ExcludedSources { get; set; } = [];
    public string Accent { get; set; } = "#88C9FF";
    public PanelTheme Theme { get; set; } = PanelTheme.Graphite;
    public double BackgroundOpacity { get; set; } = 1;
    public double ImageDarken { get; set; } = .45;
    public string BackgroundImagePath { get; set; } = "";
    public string BackgroundSourcePath { get; set; } = "";
    public double MediaWidth { get; set; } = 376;
    public double PanelHeight { get; set; } = 164;
    public double Radius { get; set; } = 6;
    public bool ShowBorder { get; set; } = true;
    public bool ShowArtwork { get; set; } = true;
    public bool ShowSource { get; set; } = true;
    public bool ShowArtist { get; set; } = true;
    public bool ShowVolumePercent { get; set; } = true;
    public bool ShowMediaControls { get; set; } = true;
    public bool HoldOnHover { get; set; } = true;
    public bool HardwareControls { get; set; } = true;
    public bool ShowOnMediaKeys { get; set; }
    public bool ShowOnStartup { get; set; }
    public bool FadeOut { get; set; } = true;
    public int FadeMilliseconds { get; set; } = 220;
    public bool ShowWebsite { get; set; } = true;
    public bool ShowTrayIcon { get; set; } = true;
    public bool ShortcutsEnabled { get; set; } = true;
    public HotkeyBinding ShowPanelShortcut { get; set; } = HotkeyBinding.Default(QuickCommand.ShowPanel);
    public HotkeyBinding PreviousShortcut { get; set; } = HotkeyBinding.Default(QuickCommand.Previous);
    public HotkeyBinding NextShortcut { get; set; } = HotkeyBinding.Default(QuickCommand.Next);
    public HotkeyBinding PlayPauseShortcut { get; set; } = HotkeyBinding.Default(QuickCommand.PlayPause);
    public HotkeyBinding GameInteractionShortcut { get; set; } = HotkeyBinding.Default(QuickCommand.GameInteraction);
    public IEnumerable<(QuickCommand Command, HotkeyBinding Binding)> GetShortcuts()
    {
        yield return (QuickCommand.ShowPanel, ShowPanelShortcut); yield return (QuickCommand.Previous, PreviousShortcut);
        yield return (QuickCommand.Next, NextShortcut); yield return (QuickCommand.PlayPause, PlayPauseShortcut);
        yield return (QuickCommand.GameInteraction, GameInteractionShortcut);
    }
    public void SetShortcut(QuickCommand command, HotkeyBinding binding)
    {
        switch (command)
        {
            case QuickCommand.ShowPanel: ShowPanelShortcut = binding; break;
            case QuickCommand.Previous: PreviousShortcut = binding; break;
            case QuickCommand.Next: NextShortcut = binding; break;
            case QuickCommand.PlayPause: PlayPauseShortcut = binding; break;
            case QuickCommand.GameInteraction: GameInteractionShortcut = binding; break;
        }
    }

    public void Normalize()
    {
        if (!Enum.IsDefined(Language)) Language = AppLanguage.Russian;
        if (!Enum.IsDefined(Corner)) Corner = Corner.TopLeft;
        if (!Enum.IsDefined(Monitor)) Monitor = MonitorChoice.Primary;
        Scale = double.IsFinite(Scale) ? Math.Clamp(Scale, .8, 1.8) : 1;
        OffsetX = double.IsFinite(OffsetX) ? Math.Clamp(OffsetX, 0, 20000) : 28;
        OffsetY = double.IsFinite(OffsetY) ? Math.Clamp(OffsetY, 0, 20000) : 44;
        HideSeconds = Math.Clamp(HideSeconds, 1, 20);
        VolumeStep = Math.Clamp(VolumeStep, 1, 10);
        PinnedSource ??= "";
        ExcludedSources ??= [];
        if (!System.Text.RegularExpressions.Regex.IsMatch(Accent ?? "", "^#[0-9a-fA-F]{6}$")) Accent = "#88C9FF";
        if (!Enum.IsDefined(Theme)) Theme = PanelTheme.Graphite;
        BackgroundOpacity = FiniteClamp(BackgroundOpacity, .1, 1, 1);
        ImageDarken = FiniteClamp(ImageDarken, 0, .9, .45);
        MediaWidth = FiniteClamp(MediaWidth, 300, 600, 376);
        PanelHeight = FiniteClamp(PanelHeight, 144, 224, 164);
        Radius = FiniteClamp(Radius, 0, 22, 6);
        BackgroundImagePath ??= "";
        BackgroundSourcePath ??= "";
        FadeMilliseconds = Math.Clamp(FadeMilliseconds, 100, 600);
        ShowPanelShortcut = HotkeyBinding.Normalize(ShowPanelShortcut, QuickCommand.ShowPanel);
        PreviousShortcut = HotkeyBinding.Normalize(PreviousShortcut, QuickCommand.Previous);
        NextShortcut = HotkeyBinding.Normalize(NextShortcut, QuickCommand.Next);
        PlayPauseShortcut = HotkeyBinding.Normalize(PlayPauseShortcut, QuickCommand.PlayPause);
        GameInteractionShortcut = HotkeyBinding.Normalize(GameInteractionShortcut, QuickCommand.GameInteraction);
    }
    private static double FiniteClamp(double value, double min, double max, double fallback) => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}

public sealed class SettingsStore
{
    public string DirectoryPath { get; }
    public string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public SettingsStore(string? directory = null) => DirectoryPath = Path.GetFullPath(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MediaBack"));
    public Settings Load()
    {
        try { var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); settings.Normalize(); return settings; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void MigrateLegacyProfile()
    {
        if (File.Exists(FilePath)) return;
        var old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetroFlyout", "settings.json");
        if (!File.Exists(old)) return;
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(old));
            if (settings == null) return;
            Directory.CreateDirectory(DirectoryPath);
            string CopyImage(string original, string name)
            {
                if (!File.Exists(original)) return "";
                var target = Path.Combine(DirectoryPath, name); File.Copy(original, target, true); return target;
            }
            settings.BackgroundImagePath = CopyImage(settings.BackgroundImagePath, "background-migrated.png");
            settings.BackgroundSourcePath = CopyImage(settings.BackgroundSourcePath, "original-migrated.png");
            Save(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }
    public void Save(Settings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(DirectoryPath);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
}
