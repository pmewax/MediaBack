using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Foundation;
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
    private readonly DispatcherTimer artworkTimer = new();
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly Dictionary<string, SessionSubscription> subscriptions = new();
    private readonly Dictionary<string, (MediaItem Item, DateTime Updated)> snapshots = new();
    private long catalogVersion;
    private DateTime lastRead;
    private bool refreshing, disposed, refreshAgain, forceAgain, queuedForce;
    public IReadOnlyList<MediaItem> Items { get; private set; } = [];
    private string? errorKey, errorDetail;
    public string? Error => errorKey == null ? null : L.Get(errorKey) + errorDetail;
    public event Action? Changed;
    public MediaSessionService()
    {
        timer.Tick += async (_, _) => await RefreshAsync(true);
        eventTimer.Tick += async (_, _) => { eventTimer.Stop(); var force = queuedForce; queuedForce = false; await RefreshAsync(force); };
        artworkTimer.Tick += async (_, _) => { artworkTimer.Stop(); await RefreshAsync(); };
    }
    private void Schedule(bool force = false)
    {
        if (disposed || dispatcher.HasShutdownStarted) return;
        dispatcher.BeginInvoke(() => { if (disposed) return; queuedForce |= force; if (!eventTimer.IsEnabled) eventTimer.Start(); });
    }
    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    { Interlocked.Increment(ref catalogVersion); Schedule(); }
    private void OnCurrentChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => Schedule();
    private async Task ConnectAsync()
    {
        if (manager != null) return;
        var connected = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        if (disposed) return;
        manager = connected; manager.SessionsChanged += OnSessionsChanged; manager.CurrentSessionChanged += OnCurrentChanged;
    }
    private void Unsubscribe(string id)
    {
        if (subscriptions.Remove(id, out var subscription)) subscription.Dispose();
        snapshots.Remove(id);
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
        await RefreshAsync(); if (!disposed) timer.Start();
    }
    private async Task RefreshAsync(bool forceArtwork = false)
    {
        if (disposed) return;
        if (refreshing) { refreshAgain = true; forceAgain |= forceArtwork; return; }
        refreshing = true;
        try
        {
            await ConnectAsync();
            if (disposed || manager == null) return;
            var version = Interlocked.Read(ref catalogVersion);
            var list = new List<MediaItem>();
            var sessions = manager.GetSessions();
            var activeIds = sessions.Select(s => s.SourceAppUserModelId).ToHashSet();
            foreach (var id in subscriptions.Keys.Where(id => !activeIds.Contains(id)).ToArray()) Unsubscribe(id);
            foreach (var session in sessions)
            {
                var id = session.SourceAppUserModelId;
                if (!subscriptions.TryGetValue(id, out var subscription) || !subscription.Session.Equals(session))
                {
                    Unsubscribe(id); subscription = new SessionSubscription(session, () => Schedule()); subscriptions[id] = subscription;
                }
                using var read = subscription.Artwork.BeginRead();
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(read.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(2));
                    var props = await session.TryGetMediaPropertiesAsync().AsTask(timeout.Token).WaitAsync(timeout.Token);
                    if (disposed) return;
                    if (!read.IsCurrent) { refreshAgain = true; RetainSnapshot(id, list); continue; }
                    if (version != Interlocked.Read(ref catalogVersion)) { refreshAgain = true; return; }
                    var playback = session.GetPlaybackInfo();
                    var controls = playback.Controls;
                    var timeline = session.GetTimelineProperties();
                    var title = props.Title ?? "";
                    var artist = props.Artist ?? "";
                    var identity = new ArtworkIdentity(title, artist, props.AlbumTitle ?? "", props.AlbumArtist ?? "", props.TrackNumber);
                    if (!subscription.Artwork.Observe(read, identity, Environment.TickCount64)) { refreshAgain = true; RetainSnapshot(id, list); continue; }
                    var playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    var position = timeline.Position - timeline.StartTime;
                    if (playing) position += DateTimeOffset.UtcNow - timeline.LastUpdatedTime;
                    var duration = timeline.EndTime - timeline.StartTime;
                    if (position < TimeSpan.Zero) position = TimeSpan.Zero;
                    if (duration > TimeSpan.Zero && position > duration) position = duration;
                    var item = new MediaItem(id, FriendlyName(id), title, artist, playing,
                        playback.PlaybackType == MediaPlaybackType.Music, IsKnownMusicApp(id),
                        controls.IsPlayPauseToggleEnabled || (playing ? controls.IsPauseEnabled : controls.IsPlayEnabled),
                        controls.IsPreviousEnabled, controls.IsNextEnabled, position, duration, subscription.Artwork.Image,
                        BrowserSourceResolver.Resolve(id, title, props.Subtitle, props.AlbumTitle, props.AlbumArtist));
                    if (!read.IsCurrent) { refreshAgain = true; RetainSnapshot(id, list); continue; }
                    if (subscription.Artwork.NeedsArtwork(read, Environment.TickCount64, forceArtwork))
                    {
                        snapshots[id] = (item, DateTime.UtcNow);
                        Items = Items.Where(old => activeIds.Contains(old.SourceId) && old.SourceId != id).Append(item).ToArray();
                        Changed?.Invoke();
                        var artwork = await LoadCoverAsync(props.Thumbnail, subscription.Artwork, read.Token);
                        if (disposed) return;
                        if (version != Interlocked.Read(ref catalogVersion)) { refreshAgain = true; return; }
                        if (!subscription.Artwork.Complete(read, artwork.Frame, artwork.Success, Environment.TickCount64))
                        { refreshAgain = true; RetainSnapshot(id, list); continue; }
                        item = item with { Artwork = subscription.Artwork.Image };
                    }
                    if (!read.IsCurrent) { refreshAgain = true; RetainSnapshot(id, list); continue; }
                    list.Add(item); snapshots[id] = (item, DateTime.UtcNow);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    subscription.Artwork.AdvanceRetries(Environment.TickCount64);
                    if (!read.IsCurrent) refreshAgain = true;
                    RetainSnapshot(id, list);
                }
                if (disposed) return;
            }
            if (version != Interlocked.Read(ref catalogVersion)) { refreshAgain = true; return; }
            Items = list; errorKey = errorDetail = null; lastRead = DateTime.UtcNow;
        }
        catch (Exception ex) { errorKey = "MediaReadError"; errorDetail = ex.Message; if (DateTime.UtcNow - lastRead > TimeSpan.FromSeconds(4)) Items = []; Disconnect(); }
        finally
        {
            refreshing = false;
            if (!disposed)
            {
                ScheduleArtworkRetry();
                if (refreshAgain) { var force = forceAgain; refreshAgain = forceAgain = false; Schedule(force); }
            }
        }
        if (!disposed) Changed?.Invoke();
    }
    private void RetainSnapshot(string id, List<MediaItem> list)
    {
        if (snapshots.TryGetValue(id, out var snapshot) && DateTime.UtcNow - snapshot.Updated < TimeSpan.FromSeconds(4)) list.Add(snapshot.Item);
    }
    private void ScheduleArtworkRetry()
    {
        artworkTimer.Stop();
        var next = subscriptions.Values.Select(s => s.Artwork.NextRetryAt).Where(t => t.HasValue).Min();
        if (next == null) return;
        artworkTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, next.Value - Environment.TickCount64));
        artworkTimer.Start();
    }
    private static async Task<(ArtworkFrame? Frame, bool Success)> LoadCoverAsync(IRandomAccessStreamReference? reference, MediaArtworkState state, CancellationToken cancellation)
    {
        if (reference == null) return (null, true);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            using var stream = await reference.OpenReadAsync().AsTask(timeout.Token).WaitAsync(timeout.Token);
            if (stream.Size == 0 || stream.Size > 8 * 1024 * 1024) return (null, false);
            using var reader = new DataReader(stream);
            var count = await reader.LoadAsync((uint)stream.Size).AsTask(timeout.Token).WaitAsync(timeout.Token);
            if (count != stream.Size) return (null, false);
            var bytes = new byte[(int)stream.Size]; reader.ReadBytes(bytes);
            cancellation.ThrowIfCancellationRequested();
            var fingerprint = Convert.ToHexString(SHA256.HashData(bytes));
            if (state.Find(fingerprint) is ArtworkFrame cached) return (cached, true);
            using var memory = new MemoryStream(bytes);
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 160; bitmap.StreamSource = memory; bitmap.EndInit(); bitmap.Freeze();
            return (new ArtworkFrame(fingerprint, bitmap), true);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return (null, false); }
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
            if (command != MediaCommand.TogglePlayPause && subscriptions.TryGetValue(sourceId, out var subscription)) subscription.Artwork.Invalidate();
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
    public void Dispose() { disposed = true; timer.Stop(); eventTimer.Stop(); artworkTimer.Stop(); Disconnect(); snapshots.Clear(); }
    private sealed class SessionSubscription : IDisposable
    {
        public GlobalSystemMediaTransportControlsSession Session { get; }
        public MediaArtworkState Artwork { get; } = new();
        private readonly TypedEventHandler<GlobalSystemMediaTransportControlsSession, MediaPropertiesChangedEventArgs> properties;
        private readonly TypedEventHandler<GlobalSystemMediaTransportControlsSession, PlaybackInfoChangedEventArgs> playback;
        public SessionSubscription(GlobalSystemMediaTransportControlsSession session, Action schedule)
        {
            Session = session;
            properties = (_, _) => { Artwork.Invalidate(); schedule(); };
            playback = (_, _) => schedule();
            session.MediaPropertiesChanged += properties; session.PlaybackInfoChanged += playback;
        }
        public void Dispose()
        {
            Artwork.Dispose();
            try { Session.MediaPropertiesChanged -= properties; Session.PlaybackInfoChanged -= playback; } catch (Exception) { }
        }
    }
}
