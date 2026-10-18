using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MediaBack.Domain;
using Forms = System.Windows.Forms;

namespace MediaBack.UI;

public partial class OverlayWindow : Window
{
    private readonly IAudioService audio;
    private readonly Settings settings;
    private readonly DispatcherTimer hideTimer = new();
    private bool updating, isPreview, placeQueued, closed;
    private string loadedBackground = "";
    private MediaItem? item;
    private MediaItem? renderedItem;
    private bool hasRendered, renderedWebsite, renderedPreview;
    private bool fading;
    private int fadeGeneration;
    public bool IsFading => fading;
    private MediaItem demo = new("demo", L.Get("Preview"), "Midnight City", "M83 · Hurry Up, We’re Dreaming", true, true, true, false, false, false, TimeSpan.Zero, TimeSpan.Zero);
    public event Action<MediaCommand>? CommandRequested;
    public int PresentationCount { get; private set; }
    public OverlayWindow(IAudioService audio, Settings settings)
    {
        this.audio = audio; this.settings = settings;
        InitializeComponent(); ApplySettings();
        L.Changed += ApplyLanguage;
        hideTimer.Tick += (_, _) => { if (!VolumeSlider.IsMouseCaptureWithin && (!settings.HoldOnHover || !IsMouseOver)) BeginHide(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { EndInteraction(true); SnapVolumeIndicator(); hideTimer.Stop(); CancelFade(); } };
        VolumeSlider.PreviewMouseDown += (_, _) => SnapVolumeIndicator();
        VolumeSlider.LostMouseCapture += (_, _) => { if (!closed) { Update(item); if (!IsMouseOver && !IsInteracting) ReleasePanelCursor(); RestartTimer(); } };
        PreviewKeyDown += InteractionKeyDown;
        PreviewMouseDown += InteractionMouseDown;
        LostMouseCapture += InteractionCaptureLost;
        Deactivated += (_, _) => { if (IsInteracting) { EndInteraction(false); RestartTimer(); } };
        SizeChanged += (_, _) => QueuePlacement();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLongPtr(hwnd, -20, (IntPtr)(GetWindowLongPtr(hwnd, -20).ToInt64() | 0x08000000L | 0x80L));
            HwndSource.FromHwnd(hwnd)?.AddHook(HandleMessage);
        };
    }
    public void Update(MediaItem? current)
    {
        if (closed) return;
        item = current; Render(isPreview ? demo : current);
        updating = true;
        if (!VolumeSlider.IsMouseCaptureWithin) SetVolumeIndicator(audio.Volume);
        VolumeSlider.IsEnabled = audio.Available; MuteButton.IsEnabled = audio.Available;
        VolumeText.Text = audio.Available ? audio.Volume.ToString() : "—";
        MuteButton.Content = audio.Muted || audio.Volume == 0 ? "\uE74F" : "\uE767";
        updating = false;
    }
    private void Render(MediaItem? current)
    {
        if (hasRendered && ReferenceEquals(renderedItem, current) && renderedWebsite == settings.ShowWebsite && renderedPreview == isPreview) return;
        hasRendered = true; renderedItem = current; renderedWebsite = settings.ShowWebsite; renderedPreview = isPreview;
        MediaPanel.Visibility = current == null ? Visibility.Collapsed : Visibility.Visible;
        GameHint.Text = current == null ? "Esc" : L.Get("GameModeActive");
        GameHint.MaxWidth = (current == null ? 80 : 80 + settings.MediaWidth) - 32;
        if (current == null) return;
        SourceText.Text = MediaSourceLabel.For(current, settings).ToUpperInvariant();
        TitleText.Text = L.MediaTitle(current.Title); TitleText.ToolTip = TitleText.Text;
        ArtistText.Text = current.Artist;
        if (!ReferenceEquals(Cover.Source, current.Artwork)) Cover.Source = current.Artwork;
        PlayButton.Content = current.Playing ? "\uE769" : "\uE768";
        PreviousButton.IsEnabled = current.CanPrevious && !isPreview;
        NextButton.IsEnabled = current.CanNext && !isPreview;
        PlayButton.IsEnabled = current.CanPlayPause && !isPreview;
    }
    public void Reveal(bool preview = false)
    {
        if (closed) return;
        CancelFade();
        isPreview = preview; Update(item);

        if (!IsVisible) { PresentationCount++; Opacity = 1; Show(); UpdateLayout(); Place(); }
        RestartTimer();
    }
    public void BeginHide()
    {
        hideTimer.Stop();
        if (closed || !IsVisible || fading) return;
        EndInteraction(true);
        if (!settings.FadeOut) { Hide(); return; }
        fading = true;
        var generation = ++fadeGeneration;
        var animation = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(settings.FadeMilliseconds)) { FillBehavior = FillBehavior.HoldEnd };
        animation.Completed += (_, _) => { if (!closed && fading && generation == fadeGeneration) Hide(); };
        BeginAnimation(OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }
    private void CancelFade()
    {
        fadeGeneration++; fading = false; BeginAnimation(OpacityProperty, null); Opacity = 1;
    }
    public void ApplySettings()
    {
        if (closed) return;
        CancelFade();
        settings.Normalize();
        Cursor = settings.ShowCursorOnHover ? Cursors.Arrow : null;
        ForceCursor = settings.ShowCursorOnHover;
        if (!settings.ShowCursorOnHover && !IsInteracting) ReleasePanelCursor();
        if (!settings.SmoothVolume) SnapVolumeIndicator();
        PanelScale.ScaleX = PanelScale.ScaleY = settings.Scale;
        Resources["Accent"] = Brush(settings.Accent);
        ContentGrid.Height = settings.PanelHeight; MediaPanel.Width = settings.MediaWidth;
        var radius = new CornerRadius(settings.Radius);
        Root.CornerRadius = SolidLayer.CornerRadius = ImageLayer.CornerRadius = ShadeLayer.CornerRadius = Outline.CornerRadius = radius;
        SolidLayer.Background = Brush(settings.Theme switch { PanelTheme.Midnight => "#121D31", PanelTheme.Warm => "#302724", PanelTheme.Classic => "#202020", _ => "#22252B" });
        BackgroundGroup.Opacity = settings.BackgroundOpacity;
        Outline.Visibility = settings.ShowBorder ? Visibility.Visible : Visibility.Collapsed; Outline.Opacity = settings.BackgroundOpacity;
        ArtworkBorder.Visibility = settings.ShowArtwork ? Visibility.Visible : Visibility.Collapsed;
        ArtworkColumn.Width = new GridLength(settings.ShowArtwork ? 68 : 0);
        SourceRow.Height = new GridLength(settings.ShowSource ? 22 : 0);
        SourceText.Visibility = settings.ShowSource ? Visibility.Visible : Visibility.Collapsed;
        ArtistText.Visibility = settings.ShowArtist ? Visibility.Visible : Visibility.Collapsed;
        NumberRow.Height = new GridLength(settings.ShowVolumePercent ? 28 : 0);
        VolumeText.Visibility = settings.ShowVolumePercent ? Visibility.Visible : Visibility.Collapsed;
        ControlsRow.Height = new GridLength(settings.ShowMediaControls ? 36 : 0);
        ControlsPanel.Visibility = settings.ShowMediaControls ? Visibility.Visible : Visibility.Collapsed;
        if (loadedBackground != settings.BackgroundImagePath)
        {
            loadedBackground = settings.BackgroundImagePath; ImageLayer.Background = null;
            if (loadedBackground.Length > 0)
            {
                try { ImageLayer.Background = new ImageBrush(ImageTools.Load(loadedBackground)) { Stretch = Stretch.UniformToFill }; }
                catch (Exception) { }
            }
        }
        ShadeLayer.Opacity = ImageLayer.Background == null ? 0 : settings.ImageDarken;
        if (IsVisible) { UpdateLayout(); QueuePlacement(); RestartTimer(); }
    }
    private static SolidColorBrush Brush(string value) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); brush.Freeze(); return brush; }
    private void QueuePlacement()
    {
        if (!IsVisible || placeQueued || closed) return;
        placeQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => { placeQueued = false; if (IsVisible && !closed) Place(); });
    }
    private void Place()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var screen = settings.Monitor == MonitorChoice.Cursor ? Forms.Screen.FromPoint(Forms.Cursor.Position) : Forms.Screen.PrimaryScreen!;
        var work = screen.WorkingArea;
        var center = new NativePoint { X = screen.Bounds.Left + screen.Bounds.Width / 2, Y = screen.Bounds.Top + screen.Bounds.Height / 2 };
        var monitor = MonitorFromPoint(center, 2);
        var dpi = GetDpiForWindow(hwnd) / 96d;
        if (MonitorFromWindow(hwnd, 2) != monitor && GetDpiForMonitor(monitor, 0, out var dx, out _) == 0) dpi = dx / 96d;
        var width = (int)Math.Ceiling(ActualWidth * dpi); var height = (int)Math.Ceiling(ActualHeight * dpi);
        var x = work.Left + (int)(settings.OffsetX * dpi); var y = work.Top + (int)(settings.OffsetY * dpi);
        if (settings.Corner is Corner.TopRight or Corner.BottomRight) x = work.Right - width - (int)(settings.OffsetX * dpi);
        if (settings.Corner is Corner.BottomLeft or Corner.BottomRight) y = work.Bottom - height - (int)(settings.OffsetY * dpi);
        x = Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - width));
        y = Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - height));
        GetWindowRect(hwnd, out var current);
        if (current.Left != x || current.Top != y) SetWindowPos(hwnd, (IntPtr)(-1), x, y, 0, 0, 0x0011);
    }
    private void RestartTimer()
    {
        CancelFade();
        hideTimer.Stop();
        if (VolumeSlider.IsMouseCaptureWithin || (settings.HoldOnHover && IsMouseOver)) return;
        hideTimer.Interval = TimeSpan.FromSeconds(settings.HideSeconds); hideTimer.Start();
    }
    private void OnMouseEnterPanel(object sender, System.Windows.Input.MouseEventArgs e) { EnsurePanelCursor(); CancelFade(); if (settings.HoldOnHover) hideTimer.Stop(); else RestartTimer(); }
    private void OnMouseLeavePanel(object sender, System.Windows.Input.MouseEventArgs e) { if (!IsMouseCaptureWithin && !IsInteracting) ReleasePanelCursor(); RestartTimer(); }
    private void OnWheel(object sender, MouseWheelEventArgs e) { audio.Adjust(Math.Sign(e.Delta) * settings.VolumeStep); Update(item); RestartTimer(); e.Handled = true; }
    private void VolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (!updating && IsLoaded) { audio.SetVolume((int)Math.Round(e.NewValue)); VolumeText.Text = audio.Volume.ToString(); RestartTimer(); } }
    private void MuteClicked(object sender, RoutedEventArgs e) { audio.ToggleMute(); Update(item); RestartTimer(); }
    private void PreviousClicked(object sender, RoutedEventArgs e) { CommandRequested?.Invoke(MediaCommand.Previous); RestartTimer(); }
    private void PlayClicked(object sender, RoutedEventArgs e) { CommandRequested?.Invoke(MediaCommand.TogglePlayPause); RestartTimer(); }
    private void NextClicked(object sender, RoutedEventArgs e) { CommandRequested?.Invoke(MediaCommand.Next); RestartTimer(); }
    private void ApplyLanguage() { hasRendered = false; demo = demo with { AppName = L.Get("Preview") }; Update(item); }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e) { EndInteraction(true); base.OnClosing(e); }
    protected override void OnClosed(EventArgs e) { closed = true; L.Changed -= ApplyLanguage; ReleasePanelCursor(); SnapVolumeIndicator(); CancelFade(); hideTimer.Stop(); base.OnClosed(e); }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
}
