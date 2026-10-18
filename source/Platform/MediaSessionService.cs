using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;
using MediaBack.Domain;

namespace MediaBack.Platform;

public sealed class MediaSessionService : IMediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(60) };
    private readonly DispatcherTimer eventTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> subscriptions = new();
    private bool refreshAgain;
    private readonly Dictionary<string, (string Key, ImageSource? Image)> covers = new();
    private readonly Dictionary<string, (MediaItem Item, DateTime Updated)> snapshots = new();
    private DateTime lastRead;
    private bool refreshing, disposed;
    public IReadOnlyList<MediaItem> Items { get; private set; } = [];
    private string? errorKey, errorDetail;
    public string? Error => errorKey == null ? null : L.Get(errorKey) + errorDetail;
    public event Action? Changed;
    public MediaSessionService()
    {
        timer.Tick += async (_, _) => await RefreshAsync();
        eventTimer.Tick += async (_, _) => { eventTimer.Stop(); await RefreshAsync(); };
    }
    private void Schedule()
    {
        if (disposed || dispatcher.HasShutdownStarted) return;
        dispatcher.BeginInvoke(() => { if (!disposed && !eventTimer.IsEnabled) eventTimer.Start(); });
    }
    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) => Schedule();
    private void OnCurrentChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => Schedule();
    private void OnPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => Schedule();
    private void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => Schedule();
    private async Task ConnectAsync()
    {
        if (manager != null) return;
        var connected = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        if (disposed) return;
        manager = connected; manager.SessionsChanged += OnSessionsChanged; manager.CurrentSessionChanged += OnCurrentChanged;
    }
    private void Unsubscribe(string id)
    {
        if (!subscriptions.Remove(id, out var session)) return;
        try { session.MediaPropertiesChanged -= OnPropertiesChanged; session.PlaybackInfoChanged -= OnPlaybackChanged; } catch (Exception) { }
    }
    private void Disconnect()
    {
        foreach (var id in subscriptions.Keys.ToArray()) Unsubscribe(id);
        if (manager != null) { try { manager.SessionsChanged -= OnSessionsChanged; manager.CurrentSessionChanged -= OnCurrentChanged; } catch (Exception) { } manager = null; }
    }
    public async Task StartAsync()
    {
        try { await ConnectAsync(); }
        catch (Exception ex) { errorKey = "MediaStartError"; errorDetail = ex.Message; }
        if (disposed) return;
        await RefreshAsync(); timer.Start();
    }
    private async Task RefreshAsync()
    {
        if (disposed) return;
        if (refreshing) { refreshAgain = true; return; }
        refreshing = true;
        try
        {
            await ConnectAsync();
            if (disposed || manager == null) return;
            var list = new List<MediaItem>();
            var sessions = manager.GetSessions();
            var activeIds = sessions.Select(s => s.SourceAppUserModelId).ToHashSet();
            foreach (var id in subscriptions.Keys.Where(id => !activeIds.Contains(id)).ToArray()) Unsubscribe(id);
            foreach (var session in sessions)
            {
                var id = session.SourceAppUserModelId;
                if (!subscriptions.TryGetValue(id, out var subscribed) || !subscribed.Equals(session))
                {
                    Unsubscribe(id); subscriptions[id] = session;
                    session.MediaPropertiesChanged += OnPropertiesChanged; session.PlaybackInfoChanged += OnPlaybackChanged;
                }
                try
                {
                    var props = await session.TryGetMediaPropertiesAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
                    var playback = session.GetPlaybackInfo();
                    var controls = playback.Controls;
                    var timeline = session.GetTimelineProperties();
                    var title = props.Title ?? "";
                    var artist = props.Artist ?? "";
                    var key = title + "\n" + artist + "\n" + props.AlbumTitle;
                    if (!covers.TryGetValue(id, out var cover) || cover.Key != key || (cover.Image == null && props.Thumbnail != null))
                    {
                        var artwork = await LoadCoverAsync(props.Thumbnail);
                        cover = (key, artwork); covers[id] = cover;
                    }
                    var playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    var position = timeline.Position - timeline.StartTime;
                    if (playing) position += DateTimeOffset.UtcNow - timeline.LastUpdatedTime;
                    var duration = timeline.EndTime - timeline.StartTime;
                    if (position < TimeSpan.Zero) position = TimeSpan.Zero;
                    if (duration > TimeSpan.Zero && position > duration) position = duration;
                    var item = new MediaItem(id, FriendlyName(id), title, artist, playing,
                        playback.PlaybackType == MediaPlaybackType.Music, IsKnownMusicApp(id),
                        controls.IsPlayPauseToggleEnabled || (playing ? controls.IsPauseEnabled : controls.IsPlayEnabled),
                        controls.IsPreviousEnabled, controls.IsNextEnabled, position, duration, cover.Image,
                        BrowserSourceResolver.Resolve(id, title, props.Subtitle, props.AlbumTitle, props.AlbumArtist));
                    list.Add(item); snapshots[id] = (item, DateTime.UtcNow);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {

                    if (snapshots.TryGetValue(id, out var snapshot) && DateTime.UtcNow - snapshot.Updated < TimeSpan.FromSeconds(4)) list.Add(snapshot.Item);
                }
                if (disposed) return;
            }
            var ids = list.Select(s => s.SourceId).ToHashSet();
            foreach (var stale in covers.Keys.Where(k => !ids.Contains(k)).ToList()) covers.Remove(stale);
            foreach (var stale in snapshots.Keys.Where(k => !ids.Contains(k)).ToList()) snapshots.Remove(stale);
            Items = list; errorKey = errorDetail = null; lastRead = DateTime.UtcNow;
        }
        catch (Exception ex) { errorKey = "MediaReadError"; errorDetail = ex.Message; if (DateTime.UtcNow - lastRead > TimeSpan.FromSeconds(4)) Items = []; Disconnect(); }
        finally { refreshing = false; if (refreshAgain && !disposed) { refreshAgain = false; Schedule(); } }
        if (!disposed) Changed?.Invoke();
    }
    private static async Task<ImageSource?> LoadCoverAsync(IRandomAccessStreamReference? reference)
    {
        if (reference == null) return null;
        try
        {
            using var stream = await reference.OpenReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            if (stream.Size > 8 * 1024 * 1024) return null;
            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)stream.Size).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            var bytes = new byte[(int)stream.Size]; reader.ReadBytes(bytes);
            using var memory = new MemoryStream(bytes);
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 160; bitmap.StreamSource = memory; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
        }
        catch (Exception) { return null; }
    }
    public async Task<bool> CommandAsync(string sourceId, MediaCommand command)
    {
        try
        {
            var session = manager?.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId == sourceId);
            if (session == null) return false;
            var success = command switch
            {
                MediaCommand.Next => await session.TrySkipNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2)),
                MediaCommand.Previous => await session.TrySkipPreviousAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2)),
                _ => await ToggleAsync(session)
            };
            await RefreshAsync(); return success;
        }
        catch (Exception) { return false; }
    }
    private static async Task<bool> ToggleAsync(GlobalSystemMediaTransportControlsSession session)
    {
        var info = session.GetPlaybackInfo();
        if (info.Controls.IsPlayPauseToggleEnabled) return await session.TryTogglePlayPauseAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        return info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
            ? await session.TryPauseAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2))
            : await session.TryPlayAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }
    public static bool IsKnownMusicApp(string id) => new[] { "spotify", "soundcloud", "foobar", "musicbee", "aimp", "deezer", "tidal", "applemusic", "yandexmusic", "yandex.music" }.Any(n => id.Contains(n, StringComparison.OrdinalIgnoreCase));
    public static string FriendlyName(string id)
    {
        foreach (var pair in new[] { ("spotify", "Spotify"), ("soundcloud", "SoundCloud"), ("telegram", "Telegram"), ("msedge", "Microsoft Edge"), ("chrome", "Google Chrome"), ("firefox", "Firefox"), ("foobar", "foobar2000"), ("musicbee", "MusicBee"), ("aimp", "AIMP"), ("yandex", "Яндекс Музыка"), ("applemusic", "Apple Music"), ("vlc", "VLC") })
            if (id.Contains(pair.Item1, StringComparison.OrdinalIgnoreCase)) return pair.Item2;
        return id.Length > 42 ? id[..39] + "…" : id;
    }
    public void Dispose() { disposed = true; timer.Stop(); eventTimer.Stop(); Disconnect(); covers.Clear(); snapshots.Clear(); }
}
