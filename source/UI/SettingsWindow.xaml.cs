using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MediaBack.Domain;
using MediaBack.Platform;

namespace MediaBack.UI;

public partial class SettingsWindow : Window
{
    private readonly Settings settings;
    private readonly string profileDirectory;
    private bool loading = true;
    private string sourcesKey = "";
    private string? apiError;
    private int currentPage;
    private IReadOnlyList<MediaItem> lastSources = [];
    private MediaItem? lastSelected;
    private bool lastAudioAvailable = true;
    private static readonly string[] accents = ["#88C9FF", "#0078D7", "#C7A0EB", "#87DCC0", "#F1B899", "#E6E6E6"];
    public event Action? SettingsChanged;
    public event Action? PreviewRequested;
    public event Action? ExitRequested;
    public event Action? ShutdownRequested;
    public event Action? TrayRequested;
    public event Action<bool>? StartupChanged;
    private sealed record SourceOption(string Id, string Label) { public override string ToString() => Label; }
    public SettingsWindow(Settings settings, string? profileDirectory = null)
    {
        this.settings = settings;
        this.profileDirectory = profileDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MediaBack");
        L.SetLanguage(settings.Language);
        InitializeComponent();
        WindowAppearance.UseDarkFrame(this);
        FillControls(); loading = false; UpdateLabels(); UpdateBackgroundPreview();
        SourceBox.DropDownClosed += (_, _) => UpdateSources(lastSources, lastSelected, apiError, lastAudioAvailable);
        Height = Math.Max(MinHeight, Math.Min(Height, SystemParameters.WorkArea.Height - 32));
        Loaded += (_, _) => ShowSupportGreeting();
        L.Changed += ApplyLanguage;
        Closed += (_, _) => { L.Changed -= ApplyLanguage; CloseSupportDialog(); CancelShortcutCapture(); };
    }
    private void FillControls()
    {
        LanguageBox.SelectedIndex = (int)settings.Language;
        CursorHoverBox.IsChecked = settings.ShowCursorOnHover;
        SmoothVolumeBox.IsChecked = settings.SmoothVolume;
        CornerBox.SelectedIndex = (int)settings.Corner; MonitorBox.SelectedIndex = (int)settings.Monitor; ThemeBox.SelectedIndex = (int)settings.Theme;
        XBox.Text = settings.OffsetX.ToString(CultureInfo.InvariantCulture); YBox.Text = settings.OffsetY.ToString(CultureInfo.InvariantCulture);
        ScaleSlider.Value = settings.Scale * 100; TimeoutSlider.Value = settings.HideSeconds; StepSlider.Value = settings.VolumeStep;
        WidthSlider.Value = settings.MediaWidth; HeightSlider.Value = settings.PanelHeight; RadiusSlider.Value = settings.Radius;
        OpacitySlider.Value = settings.BackgroundOpacity * 100; ShadeSlider.Value = settings.ImageDarken * 100;
        MusicOnlyBox.IsChecked = settings.MusicOnly; VolumeHookBox.IsChecked = settings.InterceptVolume; MediaHookBox.IsChecked = settings.InterceptMedia;
        ArtworkBox.IsChecked = settings.ShowArtwork; SourceVisibleBox.IsChecked = settings.ShowSource; ArtistVisibleBox.IsChecked = settings.ShowArtist;
        VolumeNumberBox.IsChecked = settings.ShowVolumePercent; MediaControlsBox.IsChecked = settings.ShowMediaControls;
        BorderBox.IsChecked = settings.ShowBorder; HoverBox.IsChecked = settings.HoldOnHover; HardwareBox.IsChecked = settings.HardwareControls;
        ShowMediaKeysBox.IsChecked = settings.ShowOnMediaKeys; StartupBox.IsChecked = settings.ShowOnStartup;
        FadeBox.IsChecked = settings.FadeOut; FadeSlider.Value = settings.FadeMilliseconds;
        WebsiteBox.IsChecked = settings.ShowWebsite; AutostartBox.IsChecked = StartupRegistration.IsEnabled;
        TrayIconBox.IsChecked = settings.ShowTrayIcon;
        var index = Array.FindIndex(accents, a => a.Equals(settings.Accent, StringComparison.OrdinalIgnoreCase));
        AccentBox.SelectedIndex = index >= 0 ? index : 6; AccentHexBox.Text = settings.Accent;
        FillShortcuts(); UpdateShortcutStatus(null);
    }
    private void Navigate(object sender, RoutedEventArgs e)
    {
        if (AppearanceSection == null || sender is not RadioButton button || !int.TryParse(button.Tag as string, out var page)) return;
        currentPage = page;
        CancelShortcutCapture();
        var sections = new[] { AppearanceSection, PositionSection, MusicSection, ControlSection, SupportSection };
        for (var i = 0; i < sections.Length; i++) sections[i].Visibility = i == page ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = new[] { L.Get("Appearance"), L.Get("Position"), L.Get("Music"), L.Get("Controls"), L.Get("Support") }[page];
        PageSubtitle.Text = new[] { L.Get("AppearanceSubtitle"), L.Get("PositionSubtitle"), L.Get("MusicSubtitle"), L.Get("ControlsSubtitle"), L.Get("SupportSubtitle") }[page];
        AnimateIn(sections[page]);

        var parent = System.Windows.Media.VisualTreeHelper.GetParent(AppearanceSection);
        while (parent != null && parent is not ScrollViewer) parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        (parent as ScrollViewer)?.ScrollToTop();
    }
    public void UpdateSources(IReadOnlyList<MediaItem> items, MediaItem? selected, string? error, bool audioAvailable)
    {
        lastSources = items; lastSelected = selected; lastAudioAvailable = audioAvailable;
        var key = string.Join("|", items.Select(i => i.SourceId).Order()) + "|pin:" + settings.PinnedSource;

        if (key != sourcesKey && !SourceBox.IsDropDownOpen)
        {
            var wasLoading = loading;
            sourcesKey = key; loading = true;
            var options = new List<SourceOption> { new("", L.Get("AutoMusic")) };
            options.AddRange(items.GroupBy(i => i.SourceId).Select(g => new SourceOption(g.Key, g.First().AppName)));
            if (settings.PinnedSource.Length > 0 && !items.Any(i => i.SourceId == settings.PinnedSource))
                options.Add(new(settings.PinnedSource, settings.PinnedSource + L.Get("NotRunning")));
            SourceBox.ItemsSource = options; SourceBox.SelectedValue = settings.PinnedSource; loading = wasLoading;
        }
        TrackStatus.Text = selected != null ? MediaSourceLabel.For(selected, settings) + " · " + L.MediaTitle(selected.Title) + "\n" + (selected.Playing ? L.Get("Playing") : L.Get("Paused"))
            : settings.PinnedSource.Length > 0 ? L.Get("WaitingPinned")
            : items.Count > 0 ? L.Get("FilteredSessions")
            : L.Get("StartPlayer");
        ClearExcludedButton.Content = L.Get("ResetExclusionsPrefix") + settings.ExcludedSources.Length + ")";
        if (error != null) { apiError = error; NoticeText.Text = error; }
        else if (apiError != null) { if (NoticeText.Text == apiError) NoticeText.Text = ""; apiError = null; }
        UpdateAudioAvailability(audioAvailable);
    }
    public void UpdateAudioAvailability(bool audioAvailable)
    {
        if (!audioAvailable) NoticeText.Text = L.Get("NoAudio");
        else if (NoticeText.Text.StartsWith(L.Get("NoAudioPrefix"))) NoticeText.Text = "";
    }
    public void SetNotice(string message) => NoticeText.Text = message;
    private void UpdateLabels()
    {
        ScaleLabel.Text = L.Format("ScaleValue", ScaleSlider.Value);
        WidthLabel.Text = L.Format("WidthValue", WidthSlider.Value);
        HeightLabel.Text = L.Format("HeightValue", HeightSlider.Value);
        RadiusLabel.Text = L.Format("RadiusValue", RadiusSlider.Value);
        TimeoutLabel.Text = L.Format("TimeoutValue", TimeoutSlider.Value);
        StepLabel.Text = L.Format("StepValue", StepSlider.Value);
        OpacityLabel.Text = L.Format("OpacityValue", OpacitySlider.Value);
        ShadeLabel.Text = L.Format("ShadeValue", ShadeSlider.Value);
        FadeLabel.Text = L.Format("FadeValue", FadeSlider.Value);
        FadeSlider.IsEnabled = FadeBox.IsChecked == true;
    }
    private void Apply()
    {
        if (loading) return;
        var coordinatesValid = double.TryParse(XBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.IsFinite(x) && x >= 0 && x <= 20000
            && double.TryParse(YBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var yValue) && double.IsFinite(yValue) && yValue >= 0 && yValue <= 20000;
        if (coordinatesValid)
        {
            settings.OffsetX = x; settings.OffsetY = double.Parse(YBox.Text, CultureInfo.InvariantCulture);
            CoordinateHint.Text = L.Get("CoordinateHint");
        }
        else CoordinateHint.Text = L.Get("CoordinateError");
        settings.Corner = (Corner)Math.Max(0, CornerBox.SelectedIndex); settings.Monitor = (MonitorChoice)Math.Max(0, MonitorBox.SelectedIndex);
        settings.Theme = (PanelTheme)Math.Max(0, ThemeBox.SelectedIndex);
        settings.Scale = ScaleSlider.Value / 100; settings.HideSeconds = (int)TimeoutSlider.Value; settings.VolumeStep = (int)StepSlider.Value;
        settings.MediaWidth = WidthSlider.Value; settings.PanelHeight = HeightSlider.Value; settings.Radius = RadiusSlider.Value;
        settings.BackgroundOpacity = OpacitySlider.Value / 100; settings.ImageDarken = ShadeSlider.Value / 100;
        settings.MusicOnly = MusicOnlyBox.IsChecked == true; settings.InterceptVolume = VolumeHookBox.IsChecked == true; settings.InterceptMedia = MediaHookBox.IsChecked == true;
        settings.ShowArtwork = ArtworkBox.IsChecked == true; settings.ShowSource = SourceVisibleBox.IsChecked == true; settings.ShowArtist = ArtistVisibleBox.IsChecked == true;
        settings.ShowVolumePercent = VolumeNumberBox.IsChecked == true; settings.ShowMediaControls = MediaControlsBox.IsChecked == true; settings.ShowBorder = BorderBox.IsChecked == true;
        settings.HoldOnHover = HoverBox.IsChecked == true; settings.HardwareControls = HardwareBox.IsChecked == true; settings.ShowOnMediaKeys = ShowMediaKeysBox.IsChecked == true;
        settings.ShowOnStartup = StartupBox.IsChecked == true;
        settings.FadeOut = FadeBox.IsChecked == true; settings.FadeMilliseconds = (int)FadeSlider.Value;
        settings.ShowWebsite = WebsiteBox.IsChecked == true;
        settings.ShowTrayIcon = TrayIconBox.IsChecked == true;
        settings.ShowCursorOnHover = CursorHoverBox.IsChecked == true;
        settings.SmoothVolume = SmoothVolumeBox.IsChecked == true;
        var hex = AccentHexBox.Text.Trim();
        var valid = System.Text.RegularExpressions.Regex.IsMatch(hex, "^#[0-9a-fA-F]{6}$");
        AccentHexBox.BorderBrush = new SolidColorBrush(valid ? Color.FromRgb(56, 71, 93) : Color.FromRgb(224, 139, 139));
        AccentHexBox.ToolTip = valid ? L.Get("HexExample") : L.Get("HexError");
        if (valid) settings.Accent = hex;
        UpdateLabels(); SettingsChanged?.Invoke();
    }
    private void SettingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loading && ReferenceEquals(sender, AccentBox) && AccentBox.SelectedIndex is >= 0 and < 6)
        { loading = true; AccentHexBox.Text = accents[AccentBox.SelectedIndex]; loading = false; }
        Apply();
    }
    private void SettingClicked(object sender, RoutedEventArgs e) => Apply();
    private void LanguageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (loading || LanguageBox.SelectedIndex < 0) return;
        CancelShortcutCapture();
        settings.Language = (AppLanguage)LanguageBox.SelectedIndex;
        L.SetLanguage(settings.Language);
        SettingsChanged?.Invoke();
    }
    private void ApplyLanguage()
    {
        var previousLoading = loading;
        loading = true;
        UpdateLabels(); UpdateBackgroundPreview(); FillShortcuts();
        PageTitle.Text = L.Get(new[] { "Appearance", "Position", "Music", "Controls", "Support" }[currentPage]);
        PageSubtitle.Text = L.Get(new[] { "AppearanceSubtitle", "PositionSubtitle", "MusicSubtitle", "ControlsSubtitle", "SupportSubtitle" }[currentPage]);
        CoordinateHint.Text = L.Get("CoordinateHint");
        AccentHexBox.ToolTip = System.Text.RegularExpressions.Regex.IsMatch(AccentHexBox.Text.Trim(), "^#[0-9a-fA-F]{6}$") ? L.Get("HexExample") : L.Get("HexError");
        NoticeText.Text = ""; apiError = null; sourcesKey = "";
        UpdateSources(lastSources, lastSelected, null, lastAudioAvailable);
        UpdateShortcutStatus(null);
        LanguageBox.SelectedIndex = (int)L.Language;
        loading = previousLoading;
    }
    private void AutostartClicked(object sender, RoutedEventArgs e) { if (!loading) StartupChanged?.Invoke(AutostartBox.IsChecked == true); }
    private void CoordinateChanged(object sender, TextChangedEventArgs e) => Apply();
    private void SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => Apply();
    private void SourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading) return;
        settings.PinnedSource = SourceBox.SelectedValue as string ?? "";
        settings.ExcludedSources = settings.ExcludedSources.Where(s => !s.Equals(settings.PinnedSource, StringComparison.OrdinalIgnoreCase)).ToArray();
        SettingsChanged?.Invoke();
    }
    private void ExcludeClicked(object sender, RoutedEventArgs e)
    {
        if (settings.PinnedSource.Length == 0) { SetNotice(L.Get("SelectBeforeExclude")); return; }
        settings.ExcludedSources = settings.ExcludedSources.Append(settings.PinnedSource).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        settings.PinnedSource = ""; sourcesKey = "";
        SetNotice(L.Get("SourceExcluded")); SettingsChanged?.Invoke();
    }
    private void ClearExcludedClicked(object sender, RoutedEventArgs e) { settings.ExcludedSources = []; SettingsChanged?.Invoke(); }
    private void ImportBackground(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = L.Get("BackgroundTitle"), Filter = L.Get("ImageFilter"), CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) EditBackground(dialog.FileName);
    }
    private void CropBackground(object sender, RoutedEventArgs e)
    {
        var path = File.Exists(settings.BackgroundSourcePath) ? settings.BackgroundSourcePath : settings.BackgroundImagePath;
        if (File.Exists(path)) EditBackground(path);
    }
    private void EditBackground(string path)
    {
        try
        {
            var image = ImageTools.Load(path);
            var editor = new CropWindow(image, (80 + settings.MediaWidth) / settings.PanelHeight) { Owner = this };
            if (editor.ShowDialog() != true || editor.Result == null) return;
            Directory.CreateDirectory(profileDirectory);
            var output = Path.Combine(profileDirectory, "background-" + Guid.NewGuid().ToString("N") + ".png");
            ImageTools.SavePng(editor.Result, output);
            var original = Path.Combine(profileDirectory, "original-" + Guid.NewGuid().ToString("N") + ".png");
            ImageTools.SavePng(image, original);
            settings.BackgroundImagePath = output;
            settings.BackgroundSourcePath = original;
            UpdateBackgroundPreview(); SettingsChanged?.Invoke(); SetNotice(L.Get("BackgroundSaved"));
        }
        catch (Exception ex) { SetNotice(L.Get("ImageLoadError") + ex.Message); }
    }
    private void RemoveBackground(object sender, RoutedEventArgs e)
    {
        settings.BackgroundImagePath = ""; settings.BackgroundSourcePath = ""; UpdateBackgroundPreview(); SettingsChanged?.Invoke();
    }
    private void UpdateBackgroundPreview()
    {
        ImportedPreview.Source = null; CropAgainButton.IsEnabled = false;
        BackgroundName.Text = L.Get("YourImage");
        if (settings.BackgroundImagePath.Length == 0) return;
        try { ImportedPreview.Source = ImageTools.Load(settings.BackgroundImagePath); CropAgainButton.IsEnabled = true; BackgroundName.Text = L.Get("ImageSet"); }
        catch (Exception) { BackgroundName.Text = L.Get("ImageMissing"); }
    }
    private void PreviewClicked(object sender, RoutedEventArgs e) => PreviewRequested?.Invoke();
    private void TrayClicked(object sender, RoutedEventArgs e) => TrayRequested?.Invoke();
    private void ExitClicked(object sender, RoutedEventArgs e) => ExitRequested?.Invoke();
    private void ShutdownClicked(object sender, RoutedEventArgs e) => ShutdownRequested?.Invoke();
}
