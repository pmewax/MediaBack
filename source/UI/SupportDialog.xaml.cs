using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MediaBack.Domain;
using Forms = System.Windows.Forms;

namespace MediaBack.UI;

public enum SupportStep { Greeting, Coin, Network, Wallet }

public partial class SupportDialog : Window
{
    private readonly bool startedWithGreeting;
    private readonly Action<string> copyAddress;
    private string selectedAsset = "";
    private bool centerQueued;
    public SupportStep Step { get; private set; }
    public DonationWallet? SelectedWallet { get; private set; }
    public SupportDialog(bool greeting) : this(greeting, Clipboard.SetText) { }
    internal SupportDialog(bool greeting, Action<string> copyAddress)
    {
        startedWithGreeting = greeting;
        this.copyAddress = copyAddress;
        InitializeComponent();
        L.Changed += ApplyLanguage;
        Closed += (_, _) => L.Changed -= ApplyLanguage;
        MaxHeight = Math.Max(400, SystemParameters.WorkArea.Height - 36);
        SetPage(greeting ? SupportStep.Greeting : SupportStep.Coin);
        ContentRendered += (_, _) => { QueueCenter(); SettingsWindow.AnimateIn(PageBody); };
        SizeChanged += (_, _) => QueueCenter();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
    }
    private void SetPage(SupportStep step)
    {
        Step = step;
        GreetingPage.Visibility = step == SupportStep.Greeting ? Visibility.Visible : Visibility.Collapsed;
        CoinPage.Visibility = step == SupportStep.Coin ? Visibility.Visible : Visibility.Collapsed;
        NetworkPage.Visibility = step == SupportStep.Network ? Visibility.Visible : Visibility.Collapsed;
        WalletPage.Visibility = step == SupportStep.Wallet ? Visibility.Visible : Visibility.Collapsed;
        HeartBadge.Visibility = step == SupportStep.Greeting ? Visibility.Visible : Visibility.Collapsed;
        BackButton.Visibility = step == SupportStep.Greeting || (step == SupportStep.Coin && !startedWithGreeting) ? Visibility.Collapsed : Visibility.Visible;
        if (step != SupportStep.Wallet) { SelectedWallet = null; QrImage.Source = null; WalletAddress.Text = ""; }
        (DialogTitle.Text, DialogSubtitle.Text) = step switch
        {
            SupportStep.Greeting => (L.Get("GreetingTitle"), L.Get("GreetingSubtitle")),
            SupportStep.Coin => (L.Get("Support"), L.Get("CoinSubtitle")),
            SupportStep.Network => (L.Get("NetworkTitle"), L.Get("NetworkSubtitle")),
            _ => (SelectedWallet!.Asset + L.Get("SupportSuffix"), SelectedWallet.DisplayNetwork)
        };
        DialogScroll.ScrollToTop();
        if (IsLoaded) SettingsWindow.AnimateIn(PageBody);
        QueueCenter();
    }
    internal void SelectCoin(string asset)
    {
        if (asset is not ("TRX" or "USDT")) throw new ArgumentException(L.Get("UnknownCoin"), nameof(asset));
        selectedAsset = asset;
        if (asset == "TRX") SelectWallet(SupportCatalog.Wallets.Single(x => x.Asset == "TRX"));
        else { NetworkPage.ItemsSource = SupportCatalog.Wallets.Where(x => x.Asset == asset).ToArray(); SetPage(SupportStep.Network); }
    }
    private void ApplyLanguage()
    {
        if (Step == SupportStep.Wallet && SelectedWallet is { } wallet) SelectWallet(wallet);
        else SetPage(Step);
        NetworkPage.Items.Refresh();
    }
    internal void SelectWallet(DonationWallet wallet)
    {
        if (wallet.Asset != selectedAsset || !SupportCatalog.Wallets.Contains(wallet)) throw new ArgumentException(L.Get("WrongNetwork"), nameof(wallet));
        SelectedWallet = wallet;
        QrImage.Source = null;
        WalletAddress.Text = wallet.Address;
        try { QrImage.Source = WalletQrImages.ForAddress(wallet.Address); WalletNotice.Text = L.Get("QrNoticePrefix") + wallet.Asset + L.Get("QrNoticeSuffix"); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { WalletNotice.Text = L.Get("QrUnavailable"); }
        CopyAddressButton.Content = L.Get("CopyAddress");
        SetPage(SupportStep.Wallet);
    }
    internal void Back()
    {
        switch (Step)
        {
            case SupportStep.Wallet: SetPage(selectedAsset == "USDT" ? SupportStep.Network : SupportStep.Coin); break;
            case SupportStep.Network: SetPage(SupportStep.Coin); break;
            case SupportStep.Coin when startedWithGreeting: SetPage(SupportStep.Greeting); break;
        }
    }
    private void GreetingSupportClicked(object sender, RoutedEventArgs e) => SetPage(SupportStep.Coin);
    private void TrxClicked(object sender, RoutedEventArgs e) => SelectCoin("TRX");
    private void UsdtClicked(object sender, RoutedEventArgs e) => SelectCoin("USDT");
    private void NetworkClicked(object sender, RoutedEventArgs e) { if (sender is Button { DataContext: DonationWallet wallet }) SelectWallet(wallet); }
    private void BackClicked(object sender, RoutedEventArgs e) => Back();
    private void CloseClicked(object sender, RoutedEventArgs e) => Close();
    private void CopyClicked(object sender, RoutedEventArgs e)
    {
        if (SelectedWallet == null) return;
        try { copyAddress(SelectedWallet.Address); CopyAddressButton.Content = L.Get("AddressCopied"); }
        catch (ExternalException) { WalletNotice.Text = L.Get("ClipboardBusy"); }
    }
    private void QueueCenter()
    {
        if (centerQueued || !IsLoaded) return;
        centerQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => { centerQueued = false; if (IsVisible) CenterOnScreen(); });
    }
    private void CenterOnScreen()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var area = Forms.Screen.FromHandle(hwnd).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        MaxHeight = Math.Max(320, area.Height / dpi.DpiScaleY - 32);
        MaxWidth = Math.Max(380, area.Width / dpi.DpiScaleX - 32);
        GetWindowRect(hwnd, out var rect);
        SetWindowPos(hwnd, IntPtr.Zero, area.Left + (area.Width - rect.Right + rect.Left) / 2,
            area.Top + (area.Height - rect.Bottom + rect.Top) / 2, 0, 0, 0x0015);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
