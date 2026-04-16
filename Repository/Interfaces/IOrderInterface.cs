// ────────────────────────────────────────────────────────────────────────
// Repository/Interfaces/IOrderInterface.cs
// Uses existing t_download_log — max 5 downloads per buyer per artwork.
// No t_download_token table needed.
// ────────────────────────────────────────────────────────────────────────
using Repository.Models;

namespace Repository.Interfaces;

public interface IOrderInterface
{
    Task<t_OrderDetail?> GetOrderDetailAsync(int orderId, int buyerId);
    Task<List<t_OrderDetail>> GetOrdersByBuyerAsync(int buyerId);

    /// <summary>How many times this buyer has downloaded this artwork (max 5).</summary>
    Task<int> GetDownloadCountAsync(int buyerId, int artworkId);

    /// <summary>Log one download into t_download_log.</summary>
    Task LogDownloadAsync(int orderId, int artworkId, int buyerId);

    /// <summary>Verify buyer completed a purchase for this artwork.</summary>
    Task<bool> BuyerOwnsArtworkAsync(int buyerId, int artworkId);

    /// <summary>Return the c_original_path for an artwork.</summary>
    Task<string?> GetOriginalPathAsync(int artworkId);
}
