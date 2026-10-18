using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MediaBack.UI;

public static class ImageTools
{
    public static BitmapSource Load(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (input.Length > 40 * 1024 * 1024) throw new InvalidDataException(L.Get("ImageTooLargeBytes"));
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        var frame = decoder.Frames[0];
        if ((long)frame.PixelWidth * frame.PixelHeight > 100_000_000) throw new InvalidDataException(L.Get("ImageTooLargePixels"));
        var width = frame.PixelWidth; var height = frame.PixelHeight;
        input.Position = 0;
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (width >= height) bitmap.DecodePixelWidth = Math.Min(width, 3000); else bitmap.DecodePixelHeight = Math.Min(height, 3000);
        bitmap.StreamSource = input; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
    }
    public static Rect CropBounds(double sourceWidth, double sourceHeight, double viewportWidth, double viewportHeight, double zoom, double left, double top)
    {
        var factor = Math.Max(viewportWidth / sourceWidth, viewportHeight / sourceHeight) * Math.Clamp(zoom, 1, 4);
        var width = sourceWidth * factor; var height = sourceHeight * factor;
        return new Rect(Math.Clamp(left, viewportWidth - width, 0), Math.Clamp(top, viewportHeight - height, 0), width, height);
    }
    public static BitmapSource ExportCrop(BitmapSource image, Rect placement, double viewportWidth, double viewportHeight)
    {
        var targetWidth = 1600; var ratio = targetWidth / viewportWidth;
        var targetHeight = Math.Max(1, (int)Math.Round(viewportHeight * ratio));
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(System.Windows.Media.Brushes.Black, null, new Rect(0, 0, targetWidth, targetHeight));
            dc.DrawImage(image, new Rect(placement.X * ratio, placement.Y * ratio, placement.Width * ratio, placement.Height * ratio));
        }
        var result = new RenderTargetBitmap(targetWidth, targetHeight, 96, 96, PixelFormats.Pbgra32); result.Render(visual); result.Freeze(); return result;
    }
    public static void SavePng(BitmapSource source, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
        using var output = File.Create(path); encoder.Save(output);
    }
}
