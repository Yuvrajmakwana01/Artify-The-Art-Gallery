using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repository.Models;

namespace Repository.Interfaces
{
    public interface IBuyerOrderInterface
    {
        /// <summary>
    /// Returns lightweight summaries of all orders belonging to <paramref name="buyerId"/>,
    /// newest first. Each row includes item count and a comma-separated title/artist preview.
    /// </summary>
    Task<List<OrderSummary>> GetOrderSummariesAsync(int buyerId);

    /// <summary>
    /// Returns the full order (header + all line items with artwork/artist details)
    /// for the given <paramref name="orderId"/> that belongs to <paramref name="buyerId"/>.
    /// Returns <c>null</c> if not found or doesn't belong to the buyer.
    /// </summary>
    Task<t_OrderHistory?> GetOrderDetailAsync(int orderId, int buyerId);
    }
}