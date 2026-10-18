namespace MediaBack.Domain;

public sealed record DonationWallet(string Asset, string Network, string Address)
{
    public string DisplayNetwork => Asset == "TRX" ? L.Get("NativeTrx") : Network;
}



public static class SupportCatalog
{
    public static IReadOnlyList<DonationWallet> Wallets { get; } = Array.AsReadOnly(new[]
    {
        new DonationWallet("TRX", "Tron · native TRX", "TURJryLPSm91EeDJoAHN6bpGPrkLTJDAoc"),
        new DonationWallet("USDT", "Ethereum · ERC-20", "0xC74F8B058e937007a40978808E073418dA1132fd"),
        new DonationWallet("USDT", "Tron · TRC-20", "TURJryLPSm91EeDJoAHN6bpGPrkLTJDAoc"),
        new DonationWallet("USDT", "Solana · SPL", "3pt1fwASJfbQtowHVzgB9bxEUrEpBFoN9SnqXuR99D2K"),
        new DonationWallet("USDT", "Polygon", "0xC74F8B058e937007a40978808E073418dA1132fd"),
        new DonationWallet("USDT", "BNB Smart Chain · BEP-20", "0xC74F8B058e937007a40978808E073418dA1132fd")
    });
}
