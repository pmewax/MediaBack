using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace MediaBack.UI;

public partial class OverlayWindow
{
    private int volumeTarget = -1, volumeAnimationGeneration;
    private Track? volumeTrack;
    internal double DisplayedVolume => GetVolumeTrack()?.Value ?? VolumeSlider.Value;
    private Track? GetVolumeTrack()
    {
        VolumeSlider.ApplyTemplate();
        var current = VolumeSlider.Template.FindName("PART_Track", VolumeSlider) as Track;
        if (!ReferenceEquals(current, volumeTrack))
        {
            SnapVolumeIndicator();
            volumeTrack = current;
        }
        return current;
    }
    private void SetVolumeIndicator(int target)
    {
        target = Math.Clamp(target, 0, 100);
        if (target == volumeTarget && Math.Abs(VolumeSlider.Value - target) < .001) return;
        var track = GetVolumeTrack();
        var from = track?.Value ?? VolumeSlider.Value;
        var animate = settings.SmoothVolume && SystemParameters.ClientAreaAnimation && IsVisible && volumeTarget >= 0 && track != null;
        volumeTarget = target;


        VolumeSlider.Value = target;
        if (!animate) { SnapVolumeIndicator(); return; }
        var generation = ++volumeAnimationGeneration;
        var animation = new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(90))
        { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        Timeline.SetDesiredFrameRate(animation, 60);
        animation.Completed += (_, _) => { if (!closed && generation == volumeAnimationGeneration) track!.BeginAnimation(Track.ValueProperty, null); };
        track!.BeginAnimation(Track.ValueProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }
    private void SnapVolumeIndicator()
    {
        volumeAnimationGeneration++;
        volumeTrack?.BeginAnimation(Track.ValueProperty, null);
    }
}
