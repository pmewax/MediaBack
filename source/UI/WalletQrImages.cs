using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MediaBack.UI;



public static class WalletQrImages
{
    private sealed record Entry(string Address, string Resource, int Modules, string Sha256);
    private static readonly Lazy<Entry[]> entries = new(() =>
    {
        using var data = System.Windows.Application.GetResourceStream(new Uri("/Assets/Support/wallet-qr.json", UriKind.Relative))!.Stream;
        return JsonSerializer.Deserialize<Entry[]>(data) ?? [];
    });
    private static readonly Dictionary<string, BitmapSource> cache = new(StringComparer.Ordinal);
    public static BitmapSource ForAddress(string address)
    {
        if (cache.TryGetValue(address, out var image)) return image;
        var entry = entries.Value.SingleOrDefault(x => x.Address == address) ?? throw new InvalidOperationException(L.Get("QrMissing"));
        using var stream = System.Windows.Application.GetResourceStream(new Uri("/Assets/Support/" + entry.Resource, UriKind.Relative))!.Stream;
        using var bytes = new MemoryStream(); stream.CopyTo(bytes); var data = bytes.ToArray();
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(data)), entry.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(L.Get("QrInvalid"));
        using var input = new MemoryStream(data);
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = input; bitmap.EndInit(); bitmap.Freeze();
        if (bitmap.PixelWidth != (entry.Modules + 8) * 10 || bitmap.PixelHeight != bitmap.PixelWidth)
            throw new InvalidOperationException(L.Get("QrSize"));
        cache[address] = bitmap;
        return bitmap;
    }
}
