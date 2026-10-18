namespace MediaBack.Domain;

public static class MediaSourceLabel
{
    public static string For(MediaItem item, Settings settings) => settings.ShowWebsite && !string.IsNullOrWhiteSpace(item.Website)
        ? item.Website + " · " + item.AppName : item.AppName;

    public static string? FromMetadata(params string?[] values)
    {
        foreach (var raw in values)
        {
            var value = (raw ?? "").Trim();
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            {
                var host = uri.IdnHost.ToLowerInvariant();
                if (HostIs(host, "youtube.com") || HostIs(host, "youtu.be")) return "YouTube";
                if (HostIs(host, "soundcloud.com")) return "SoundCloud";
                if (HostIs(host, "vk.ru") || HostIs(host, "vk.com") || HostIs(host, "vkvideo.ru")) return "VK";
                if (HostIs(host, "music.yandex.ru")) return "Яндекс Музыка";
                if (HostIs(host, "open.spotify.com")) return "Spotify";
            }
            if (value.Equals("YouTube", StringComparison.OrdinalIgnoreCase)) return "YouTube";
            if (value.Equals("SoundCloud", StringComparison.OrdinalIgnoreCase)) return "SoundCloud";
            if (value.Equals("VK", StringComparison.OrdinalIgnoreCase) || value.Equals("VK Музыка", StringComparison.OrdinalIgnoreCase)) return "VK";
        }
        return null;
    }
    private static bool HostIs(string host, string domain) => host == domain || host.EndsWith("." + domain, StringComparison.Ordinal);

    public static string? FromMatchingWindow(string trackTitle, string windowTitle)
    {

        if (trackTitle.Length < 5 || !windowTitle.Contains(trackTitle, StringComparison.OrdinalIgnoreCase)) return null;
        foreach (var site in new[] { "YouTube", "SoundCloud", "VK Музыка", "VK Видео", "VK Video", "vk.ru", "vk.com" })
        {
            if (windowTitle.Contains(" - " + site, StringComparison.OrdinalIgnoreCase)
                || windowTitle.Contains(" — " + site, StringComparison.OrdinalIgnoreCase)
                || windowTitle.StartsWith(site + " — ", StringComparison.OrdinalIgnoreCase))
                return site.StartsWith("VK", StringComparison.OrdinalIgnoreCase) || site.StartsWith("vk.", StringComparison.OrdinalIgnoreCase) ? "VK" : site;
        }
        return null;
    }
}
