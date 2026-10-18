using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MediaBack.UI;

public partial class CropWindow : Window
{
    private readonly BitmapSource image;
    private Point? dragStart;
    private Rect placement;
    private double startX, startY;
    public BitmapSource? Result { get; private set; }
    public CropWindow(BitmapSource image, double aspect)
    {
        this.image = image; InitializeComponent();
        WindowAppearance.UseDarkFrame(this);
        aspect = Math.Clamp(aspect, 1, 6);
        Viewport.Width = Guides.Width = Math.Min(680, 300 * aspect);
        Viewport.Height = Guides.Height = Viewport.Width / aspect;
        Height = Math.Max(570, Viewport.Height + 325);
        Picture.Source = image; Center();
        L.Changed += Render;
        Closed += (_, _) => L.Changed -= Render;
    }
    private void Center()
    {
        var rect = ImageTools.CropBounds(image.PixelWidth, image.PixelHeight, Viewport.Width, Viewport.Height, ZoomSlider.Value, 0, 0);
        placement = new Rect((Viewport.Width - rect.Width) / 2, (Viewport.Height - rect.Height) / 2, rect.Width, rect.Height); Render();
    }
    private void Render()
    {
        Picture.Width = placement.Width; Picture.Height = placement.Height;
        Canvas.SetLeft(Picture, placement.X); Canvas.SetTop(Picture, placement.Y);
        ZoomLabel.Text = L.Format("ZoomValue", ZoomSlider.Value * 100);
    }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Picture == null || image == null || placement.Width == 0) return;
        var centerX = (Viewport.Width / 2 - placement.X) / placement.Width;
        var centerY = (Viewport.Height / 2 - placement.Y) / placement.Height;
        var rect = ImageTools.CropBounds(image.PixelWidth, image.PixelHeight, Viewport.Width, Viewport.Height, e.NewValue, 0, 0);
        placement = ImageTools.CropBounds(image.PixelWidth, image.PixelHeight, Viewport.Width, Viewport.Height, e.NewValue, Viewport.Width / 2 - centerX * rect.Width, Viewport.Height / 2 - centerY * rect.Height); Render();
    }
    private void StartDrag(object sender, MouseButtonEventArgs e) { dragStart = e.GetPosition(Viewport); startX = placement.X; startY = placement.Y; Viewport.CaptureMouse(); e.Handled = true; }
    private void MoveDrag(object sender, MouseEventArgs e)
    {
        if (dragStart == null) return;
        var point = e.GetPosition(Viewport);
        placement = ImageTools.CropBounds(image.PixelWidth, image.PixelHeight, Viewport.Width, Viewport.Height, ZoomSlider.Value, startX + point.X - dragStart.Value.X, startY + point.Y - dragStart.Value.Y); Render();
    }
    private void EndDrag(object sender, MouseButtonEventArgs e) { dragStart = null; Viewport.ReleaseMouseCapture(); }
    private void LostCapture(object sender, MouseEventArgs e) => dragStart = null;
    private void ZoomWheel(object sender, MouseWheelEventArgs e) { ZoomSlider.Value = Math.Clamp(ZoomSlider.Value + Math.Sign(e.Delta) * .1, 1, 4); e.Handled = true; }
    private void CenterClicked(object sender, RoutedEventArgs e) => Center();
    private void SaveClicked(object sender, RoutedEventArgs e) { Result = ImageTools.ExportCrop(image, placement, Viewport.Width, Viewport.Height); DialogResult = true; }
}
