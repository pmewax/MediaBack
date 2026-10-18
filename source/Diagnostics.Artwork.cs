using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaBack.Platform;
using MediaBack.UI;

namespace MediaBack;

internal static partial class Diagnostics
{
    private static async Task RunArtworkChecksAsync(Action<string, bool> check)
    {
        var firstImage = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 255 }, 4); firstImage.Freeze();
        var secondImage = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 255, 0, 0, 255 }, 4); secondImage.Freeze();
        var first = new ArtworkFrame("cover-a", firstImage);
        var second = new ArtworkFrame("cover-b", secondImage);
        var trackA = new ArtworkIdentity("Track A", "Artist", "Album", "Artist", 1);
        var trackB = new ArtworkIdentity("Track B", "Artist", "Album", "Artist", 2);
        using var state = new MediaArtworkState();
        using (var read = state.BeginRead())
        {
            check("First track starts without an inherited cover", state.Observe(read, trackA, 0) && state.Image == null && state.NeedsArtwork(read, 0));
            check("First cover is accepted for its metadata", state.Complete(read, first, true, 0) && ReferenceEquals(state.Image, firstImage));
            check("Unchanged metadata reuses its decoded artwork", !state.NeedsArtwork(read, 100) && ReferenceEquals(state.Find("cover-a"), first));
        }
        state.Invalidate();
        using (var read = state.BeginRead())
        {
            state.Observe(read, trackB, 1000);
            check("Changing track clears the old cover before its replacement loads", state.Image == null);
            state.Complete(read, first, true, 1000);
            check("Late previous-track bytes do not become the new track cover", state.Image == null && state.NextRetryAt == 1350);
        }
        state.Invalidate();
        using (var read = state.BeginRead())
        {
            state.Observe(read, trackB, 1200);
            check("An artwork-only event invalidates the same-title cache", state.NeedsArtwork(read, 1200));
            state.Complete(read, second, true, 1200);
            check("Corrected artwork with unchanged title and artist replaces the stale bytes", ReferenceEquals(state.Image, secondImage));
        }
        using (var oldRead = state.BeginRead())
        {
            state.Observe(oldRead, trackB, 1300);
            await Task.Run(state.Invalidate);
            check("A metadata event cancels a pending read across threads", oldRead.Token.IsCancellationRequested && !oldRead.IsCurrent);
            using var latest = state.BeginRead(); state.Observe(latest, trackA, 1400); state.Complete(latest, first, true, 1400);
            check("An obsolete asynchronous cover cannot overwrite the current track", !state.Complete(oldRead, second, true, 1500) && ReferenceEquals(state.Image, firstImage));
        }
        using var sharedAlbum = new MediaArtworkState();
        using (var read = sharedAlbum.BeginRead()) { sharedAlbum.Observe(read, trackA, 0); sharedAlbum.Complete(read, first, true, 0); }
        sharedAlbum.Invalidate();
        using (var read = sharedAlbum.BeginRead())
        {
            sharedAlbum.Observe(read, trackB, 1000); sharedAlbum.Complete(read, first, true, 1000);
            check("The first follow-up refresh is scheduled only around a track change", sharedAlbum.NextRetryAt == 1350);
            sharedAlbum.Complete(read, first, true, 1350);
            check("Second bounded artwork retry has a later deadline", sharedAlbum.NextRetryAt == 2100 && sharedAlbum.Image == null);
            sharedAlbum.Complete(read, first, true, 2100);
            check("Third bounded artwork retry has a final deadline", sharedAlbum.NextRetryAt == 3500);
            sharedAlbum.Complete(read, first, true, 3500);
            check("Tracks sharing one album cover eventually retain that valid image", ReferenceEquals(sharedAlbum.Image, firstImage));
            check("Artwork follow-ups stop instead of polling continuously", sharedAlbum.NextRetryAt == null && !sharedAlbum.NeedsArtwork(read, 100000));
            check("The minute watchdog can recheck an unchanged thumbnail", sharedAlbum.NeedsArtwork(read, 100000, true));
        }
        using var missing = new MediaArtworkState();
        using (var read = missing.BeginRead())
        {
            missing.Observe(read, trackB, 0); missing.Complete(read, null, true, 0);
            check("A track without artwork never inherits another track's image", missing.Image == null);
            missing.AdvanceRetries(2500);
            check("Metadata failures cannot leave a busy artwork retry timer running", missing.NextRetryAt == null);
        }
        using var other = new MediaArtworkState();
        using (var read = other.BeginRead()) { other.Observe(read, trackA, 0); other.Complete(read, second, true, 0); }
        check("Identically titled tracks in separate media sessions have separate covers", ReferenceEquals(state.Image, firstImage) && ReferenceEquals(other.Image, secondImage));
        using (var read = other.BeginRead())
        {
            other.Dispose();
            check("A removed session cancels loading and rejects a late image", read.Token.IsCancellationRequested && !other.Complete(read, first, true, 100) && other.Image == null);
        }
        check("Metadata identities do not collide on embedded separator characters", new ArtworkIdentity("A\nB", "C", "", "", 0) != new ArtworkIdentity("A", "B\nC", "", "", 0));
    }

    private static void RunTooltipChecks(SettingsWindow window, OverlayWindow overlay, Action<string, bool> check)
    {
        var controls = Tree(window).Concat(Tree(overlay)).OfType<FrameworkElement>().ToArray();
        check("Flyout and settings have no assigned hover tooltips", controls.All(control => control.ToolTip == null));
        check("Application controls disable hover tooltips by default", controls.All(control => !ToolTipService.GetIsEnabled(control)));
        check("Player buttons keep screen-reader names without popups", new[] { "MuteButton", "PreviousButton", "PlayButton", "NextButton" }.All(name => AutomationProperties.GetName((DependencyObject)overlay.FindName(name)).Length > 0));
        var cover = (Image)overlay.FindName("Cover");
        var artwork = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 255 }, 4); artwork.Freeze();
        var item = new Domain.MediaItem("art-test", "Test", "With cover", "Artist", true, true, true, true, true, true, TimeSpan.Zero, TimeSpan.Zero, artwork);
        overlay.Update(item);
        check("The flyout renders the current track artwork", ReferenceEquals(cover.Source, artwork));
        overlay.Update(item with { Title = "Next without cover", Artwork = null });
        check("The flyout clears artwork for a new track without an image", cover.Source == null);
        overlay.Update(item); overlay.Update(null);
        check("Closing a media session clears the hidden artwork reference", cover.Source == null);
    }
}
