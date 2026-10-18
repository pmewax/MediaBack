using System.Threading;
using System.Windows.Media;

namespace MediaBack.Platform;

internal readonly record struct ArtworkIdentity(string Title, string Artist, string Album, string AlbumArtist, int TrackNumber);
internal sealed record ArtworkFrame(string Fingerprint, ImageSource Image);

internal sealed class MediaArtworkState : IDisposable
{
    private static readonly long[] RetryOffsets = [350, 1100, 2500];
    private readonly object gate = new();
    private long revision;
    private CancellationTokenSource? activeRead;
    private bool disposed, hasIdentity, hasRead;
    private ArtworkIdentity identity;
    private long loadedRevision = -1, changedAt;
    private int nextRetry;
    private string? previousFingerprint;
    private ArtworkFrame? frame, candidate;
    public ImageSource? Image => frame?.Image;
    public long? NextRetryAt => hasIdentity && nextRetry < RetryOffsets.Length ? changedAt + RetryOffsets[nextRetry] : null;

    public void Invalidate()
    {
        CancellationTokenSource? pending;
        lock (gate) { if (disposed) return; revision++; pending = activeRead; }
        try { pending?.Cancel(); } catch (ObjectDisposedException) { }
    }

    public ReadLease BeginRead()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var cancellation = new CancellationTokenSource();
            activeRead = cancellation;
            return new ReadLease(this, revision, cancellation);
        }
    }

    public bool Observe(ReadLease read, ArtworkIdentity current, long now)
    {
        if (!read.IsCurrent) return false;
        if (!hasIdentity || identity != current)
        {
            previousFingerprint = frame?.Fingerprint ?? candidate?.Fingerprint;
            candidate = frame ?? candidate;
            frame = null; hasRead = false; loadedRevision = -1;
            identity = current; hasIdentity = true; changedAt = now; nextRetry = 0;
        }
        return true;
    }

    public bool NeedsArtwork(ReadLease read, long now, bool force = false) =>
        force || !hasRead || loadedRevision != read.Revision || NextRetryAt is long retry && now >= retry;

    public ArtworkFrame? Find(string fingerprint) =>
        frame?.Fingerprint == fingerprint ? frame : candidate?.Fingerprint == fingerprint ? candidate : null;

    public bool Complete(ReadLease read, ArtworkFrame? current, bool success, long now)
    {
        if (!read.IsCurrent) return false;
        hasRead = true; loadedRevision = read.Revision;
        if (success)
        {
            candidate = current;
            frame = current != null && current.Fingerprint == previousFingerprint && now < changedAt + RetryOffsets[^1]
                ? null : current;
        }
        AdvanceRetries(now);
        return true;
    }

    public void AdvanceRetries(long now) { while (NextRetryAt is long retry && now >= retry) nextRetry++; }

    private bool IsCurrent(long version) { lock (gate) return !disposed && revision == version; }
    private void EndRead(CancellationTokenSource cancellation)
    {
        lock (gate) { if (ReferenceEquals(activeRead, cancellation)) activeRead = null; }
        cancellation.Dispose();
    }
    public void Dispose()
    {
        CancellationTokenSource? pending;
        lock (gate) { if (disposed) return; disposed = true; revision++; pending = activeRead; activeRead = null; }
        try { pending?.Cancel(); } catch (ObjectDisposedException) { }
        frame = candidate = null;
    }

    internal sealed class ReadLease : IDisposable
    {
        private readonly MediaArtworkState owner;
        private readonly CancellationTokenSource cancellation;
        private bool disposed;
        public long Revision { get; }
        public CancellationToken Token { get; }
        public bool IsCurrent => !disposed && owner.IsCurrent(Revision) && !Token.IsCancellationRequested;
        internal ReadLease(MediaArtworkState owner, long revision, CancellationTokenSource cancellation)
        { this.owner = owner; Revision = revision; this.cancellation = cancellation; Token = cancellation.Token; }
        public void Dispose() { if (disposed) return; disposed = true; owner.EndRead(cancellation); }
    }
}
