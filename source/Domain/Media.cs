using System.Windows.Media;

namespace MediaBack.Domain;

public sealed record MediaItem(string SourceId, string AppName, string Title, string Artist, bool Playing,
    bool IsMusic, bool KnownMusicApp, bool CanPlayPause, bool CanPrevious, bool CanNext,
    TimeSpan Position, TimeSpan Duration, ImageSource? Artwork = null, string? Website = null);

public static class SessionSelector
{
    public static MediaItem? Select(IEnumerable<MediaItem> items, Settings settings, string? previous)
    {
        var candidates = items.Where(s => !settings.ExcludedSources.Contains(s.SourceId, StringComparer.OrdinalIgnoreCase)).ToList();
        if (settings.PinnedSource.Length > 0)
            return candidates.FirstOrDefault(s => s.SourceId.Equals(settings.PinnedSource, StringComparison.OrdinalIgnoreCase));

        if (settings.MusicOnly) candidates = candidates.Where(s => s.IsMusic || s.KnownMusicApp).ToList();
        return candidates.OrderByDescending(s => s.Playing)
            .ThenByDescending(s => s.KnownMusicApp)
            .ThenByDescending(s => s.SourceId == previous)
            .ThenBy(s => s.SourceId, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }
}

public interface IAudioService : IDisposable
{
    int Volume { get; }
    bool Muted { get; }
    bool Available { get; }
    event Action? Changed;
    void Refresh();
    void SetVolume(int volume);
    void Adjust(int delta);
    void ToggleMute();
}

public interface IMediaService : IDisposable
{
    IReadOnlyList<MediaItem> Items { get; }
    string? Error { get; }
    event Action? Changed;
    Task StartAsync();
    Task<bool> CommandAsync(string sourceId, MediaCommand command);
}
public enum MediaCommand { Previous, TogglePlayPause, Next }
