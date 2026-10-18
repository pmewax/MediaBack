using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MediaBack.UI;

public partial class SettingsWindow
{
    private SupportDialog? supportDialog;
    private bool supportQueued, settingsClosed;
    internal bool IsSupportGreetingVisible => supportDialog is { IsVisible: true, Step: SupportStep.Greeting };
    internal SupportDialog? ActiveSupportDialog => supportDialog;
    public void ShowSupportGreeting() => QueueSupportDialog(true);
    public void ShowSupportOptions() => QueueSupportDialog(false);
    private void ChooseSupportCoin(object sender, RoutedEventArgs e) => ShowSupportOptions();
    private void QueueSupportDialog(bool greeting)
    {
        if (!IsLoaded || !IsVisible || settingsClosed) return;
        if (supportDialog != null) { supportDialog.Activate(); return; }
        if (supportQueued) return;
        supportQueued = true;


        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            supportQueued = false;
            if (settingsClosed || !IsVisible || supportDialog != null) return;
            var dialog = new SupportDialog(greeting) { Owner = this };
            supportDialog = dialog;
            try { dialog.ShowDialog(); }
            finally { if (ReferenceEquals(supportDialog, dialog)) supportDialog = null; }
        });
    }
    private void CloseSupportDialog()
    {
        settingsClosed = true;
        if (supportDialog is { IsVisible: true }) supportDialog.Close();
    }
    internal static void AnimateIn(FrameworkElement element)
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        var duration = TimeSpan.FromMilliseconds(180);
        var opacity = new DoubleAnimation(0, 1, duration) { FillBehavior = FillBehavior.Stop };
        Timeline.SetDesiredFrameRate(opacity, 30);
        element.BeginAnimation(OpacityProperty, opacity);
        if (element.RenderTransform is not TranslateTransform transform) element.RenderTransform = transform = new TranslateTransform();
        var slide = new DoubleAnimation(8, 0, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        Timeline.SetDesiredFrameRate(slide, 30); transform.BeginAnimation(TranslateTransform.YProperty, slide);
    }
}
