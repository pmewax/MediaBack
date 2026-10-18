using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace MediaBack.Localization;

public enum AppLanguage { Russian, English }



public static class L
{
    private static readonly Dictionary<string, string> russian = Load("ru"), english = Load("en");
    public static AppLanguage Language { get; private set; } = AppLanguage.Russian;
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Language == AppLanguage.English ? "en-US" : "ru-RU");
    public static event Action? Changed;
    internal static IReadOnlyDictionary<string, string> Russian => russian;
    internal static IReadOnlyDictionary<string, string> English => english;
    private static Dictionary<string, string> Load(string code)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MediaBack.Strings." + code + ".json")
            ?? throw new InvalidDataException("Missing language catalog: " + code);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? throw new InvalidDataException("Empty language catalog: " + code);
    }
    public static string Get(string key) => (Language == AppLanguage.English ? english : russian)[key];
    public static string Format(string key, params object[] values) => string.Format(Culture, Get(key), values);
    public static string MediaTitle(string title) => string.IsNullOrWhiteSpace(title) ? Get("Untitled") : title;
    public static void SetLanguage(AppLanguage language)
    {
        if (!Enum.IsDefined(language)) language = AppLanguage.Russian;
        var changed = Language != language;
        Language = language;
        var resources = System.Windows.Application.Current?.Resources;
        if (changed || (resources != null && !resources.Contains("Loc.SettingsTitle")))
        {
            if (resources != null) foreach (var (key, value) in language == AppLanguage.English ? english : russian) resources["Loc." + key] = value;
            if (changed) Changed?.Invoke();
        }
    }
}
