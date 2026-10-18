using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using MediaBack.Domain;
using MediaBack.UI;
using Forms = System.Windows.Forms;

namespace MediaBack;

internal static partial class Diagnostics
{
    private static async Task RunSupportChecksAsync(SettingsWindow owner, string output, Action<string, bool> check)
    {
        var dialog = owner.ActiveSupportDialog ?? throw new InvalidOperationException("Greeting dialog was not created.");
        check("Greeting is an owned dialog outside the taskbar", dialog.Owner == owner && !dialog.ShowInTaskbar && dialog.Step == SupportStep.Greeting);
        var origin = dialog.PointToScreen(new Point()); var dpi = VisualTreeHelper.GetDpi(dialog);
        var area = Forms.Screen.FromHandle(new WindowInteropHelper(dialog).Handle).WorkingArea;
        check("Greeting is centered on the monitor working area", Math.Abs(origin.X + dialog.ActualWidth * dpi.DpiScaleX / 2 - area.Left - area.Width / 2d) <= 2 && Math.Abs(origin.Y + dialog.ActualHeight * dpi.DpiScaleY / 2 - area.Top - area.Height / 2d) <= 2);
        var close = (Button)dialog.FindName("CloseDialogButton"); close.ApplyTemplate();
        var mark = (FrameworkElement)close.Template.FindName("CloseMark", close);
        var center = mark.TranslatePoint(new Point(mark.ActualWidth / 2, mark.ActualHeight / 2), close);
        check("Close vector is square and centered in its button", close.ActualWidth == close.ActualHeight && Math.Abs(center.X - close.ActualWidth / 2) < .5 && Math.Abs(center.Y - close.ActualHeight / 2) < .5);
        SaveVisual(dialog, System.IO.Path.Combine(output, "greeting-preview.png"));
        Click(dialog, "GreetingSupportButton"); await Task.Delay(240);
        check("Support action opens coin selection in the same dialog", owner.ActiveSupportDialog == dialog && dialog.Step == SupportStep.Coin);
        SaveVisual(dialog, System.IO.Path.Combine(output, "coin-preview.png"));
        Click(dialog, "UsdtButton"); await Task.Delay(240);
        check("USDT opens all five network choices", dialog.Step == SupportStep.Network && ((ItemsControl)dialog.FindName("NetworkPage")).Items.Count == 5);
        SaveVisual(dialog, System.IO.Path.Combine(output, "network-preview.png"));
        var index = 0;
        foreach (var wallet in SupportCatalog.Wallets.Where(x => x.Asset == "USDT"))
        {
            dialog.SelectWallet(wallet); await Task.Delay(240); dialog.UpdateLayout();
            var address = (TextBox)dialog.FindName("WalletAddress"); var qr = (Image)dialog.FindName("QrImage");
            var copy = (Button)dialog.FindName("CopyAddressButton");
            check("Correct address and QR for " + wallet.Network, dialog.Step == SupportStep.Wallet && dialog.SelectedWallet == wallet && address.Text == wallet.Address && ReferenceEquals(qr.Source, WalletQrImages.ForAddress(wallet.Address)));
            check("Address and copy button share aligned edges for " + wallet.Network, Math.Abs(address.ActualWidth - copy.ActualWidth) < .5 && Math.Abs(address.TranslatePoint(new Point(), dialog).X - copy.TranslatePoint(new Point(), dialog).X) < .5);
            SaveVisual(dialog, System.IO.Path.Combine(output, "wallet-" + index++ + ".png"));
            dialog.Back();
            check("Back clears address and QR before another network selection", dialog.Step == SupportStep.Network && qr.Source == null && address.Text == "" && dialog.SelectedWallet == null);
        }
        dialog.Back(); Click(dialog, "TrxButton"); await Task.Delay(240);
        check("TRX skips unnecessary network selection", dialog.Step == SupportStep.Wallet && dialog.SelectedWallet == SupportCatalog.Wallets.Single(x => x.Asset == "TRX"));
        SaveVisual(dialog, System.IO.Path.Combine(output, "wallet-trx.png"));
        var rejected = false;
        try { dialog.SelectWallet(SupportCatalog.Wallets.First(x => x.Asset == "USDT")); } catch (ArgumentException) { rejected = true; }
        check("Wallet selection rejects a mismatched coin", rejected && dialog.SelectedWallet!.Asset == "TRX");
        Click(dialog, "CloseDialogButton"); await Task.Delay(80);
        check("Manual close releases the owned support dialog", owner.ActiveSupportDialog == null);
        owner.ShowSupportOptions(); await Task.Delay(240);
        check("Support page can reopen directly at coin selection", owner.ActiveSupportDialog?.Step == SupportStep.Coin);
        owner.ActiveSupportDialog!.Close(); await Task.Delay(80);
        var copied = "";
        var copyDialog = new SupportDialog(false, value => copied = value);
        copyDialog.SelectCoin("TRX"); Click(copyDialog, "CopyAddressButton");
        check("Copy action uses the exact selected receiving address", copied == SupportCatalog.Wallets[0].Address && ((Button)copyDialog.FindName("CopyAddressButton")).Content.ToString()!.Contains("скопирован"));
        copyDialog.Close();
        var busyClipboard = new SupportDialog(false, _ => throw new ExternalException("Busy"));
        busyClipboard.SelectCoin("USDT"); busyClipboard.SelectWallet(SupportCatalog.Wallets[1]); Click(busyClipboard, "CopyAddressButton");
        check("Busy clipboard leaves the address available for manual copying", ((TextBlock)busyClipboard.FindName("WalletNotice")).Text.Contains("вручную") && ((TextBox)busyClipboard.FindName("WalletAddress")).Text == SupportCatalog.Wallets[1].Address);
        busyClipboard.Close();
    }
    private static void Click(Window window, string name) => ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
}
