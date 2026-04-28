using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Repository.Interfaces;
using Repository.Models;

namespace Repository.Implementations
{
    public class BuyerOrderRepository: IBuyerOrderInterface
    {
        private readonly NpgsqlConnection _conn;

        public BuyerOrderRepository(NpgsqlConnection conn)
        {
            _conn = conn;
        }

        // ── GET ALL ORDER SUMMARIES FOR A BUYER ───────────────────────────────────
        public async Task<List<OrderSummary>> GetOrderSummariesAsync(int buyerId)
        {
            var list = new List<OrderSummary>();

            const string sql = @"
            SELECT
                o.c_order_id,
                o.c_total_amount,
                o.c_order_status,
                o.c_created_at,
                COUNT(oi.c_item_id)::int                                          AS item_count,
                STRING_AGG(a.c_title,       ', ' ORDER BY oi.c_item_id)          AS preview_titles,
                STRING_AGG(ap.c_artist_name, ', ' ORDER BY oi.c_item_id)         AS preview_artists,
                (SELECT a2.c_preview_path
                    FROM   t_order_item oi2
                    JOIN   t_artwork    a2 ON a2.c_artwork_id = oi2.c_artwork_id
                    WHERE  oi2.c_order_id = o.c_order_id
                    ORDER  BY oi2.c_item_id
                    LIMIT  1)                                                        AS first_preview_path
            FROM t_order o
            JOIN t_order_item    oi  ON oi.c_order_id  = o.c_order_id
            JOIN t_artwork       a   ON a.c_artwork_id  = oi.c_artwork_id
            JOIN t_artist_profile ap ON ap.c_artist_id = a.c_artist_id
            WHERE o.c_buyer_id = @buyerId
            GROUP BY o.c_order_id, o.c_total_amount, o.c_order_status, o.c_created_at
            ORDER BY o.c_created_at DESC";

            try
            {
                await _conn.CloseAsync();
                await _conn.OpenAsync();

                using var cmd = new NpgsqlCommand(sql, _conn);
                cmd.Parameters.AddWithValue("@buyerId", buyerId);

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new OrderSummary
                    {
                        OrderId          = r.GetInt32(0),
                        TotalAmount      = r.GetDecimal(1),
                        OrderStatus      = r.GetString(2),
                        CreatedAt        = r.GetDateTime(3),
                        ItemCount        = r.GetInt32(4),
                        PreviewTitles    = r.IsDBNull(5) ? string.Empty : r.GetString(5),
                        PreviewArtists   = r.IsDBNull(6) ? string.Empty : r.GetString(6),
                        FirstPreviewPath = r.IsDBNull(7) ? null : r.GetString(7),
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetOrderSummariesAsync Error: " + ex.Message);
            }
            finally
            {
                await _conn.CloseAsync();
            }

            return list;
        }

        // ── GET FULL ORDER DETAIL ─────────────────────────────────────────────────
        public async Task<t_OrderHistory> GetOrderDetailAsync(int orderId, int buyerId)
        {
            t_Order? order = null;
            var items = new List<t_OrderItem>();

            // 1. Load order header
            const string headerSql = @"
            SELECT c_order_id, c_buyer_id, c_total_amount, c_order_status, c_created_at
            FROM   t_order
            WHERE  c_order_id = @orderId
              AND  c_buyer_id = @buyerId";

            try
            {
                await _conn.CloseAsync();
                await _conn.OpenAsync();

                using (var cmd = new NpgsqlCommand(headerSql, _conn))
                {
                    cmd.Parameters.AddWithValue("@orderId", orderId);
                    cmd.Parameters.AddWithValue("@buyerId", buyerId);

                    using var r = await cmd.ExecuteReaderAsync();
                    if (await r.ReadAsync())
                    {
                        order = new t_Order
                        {
                            OrderId     = r.GetInt32(0),
                            BuyerId     = r.GetInt32(1),
                            TotalAmount = r.GetDecimal(2),
                            OrderStatus = r.GetString(3),
                            CreatedAt   = r.GetDateTime(4),
                        };
                    }
                }

                if (order == null) return null;

                // 2. Load line items — reuse same open connection
                const string itemsSql = @"
                SELECT
                    oi.c_item_id,
                    oi.c_order_id,
                    oi.c_artwork_id,
                    oi.c_price_at_purchase,
                    a.c_title,
                    a.c_description,
                    a.c_preview_path,
                    cat.c_category_name,
                    ap.c_artist_name
                FROM  t_order_item    oi
                JOIN  t_artwork       a   ON a.c_artwork_id    = oi.c_artwork_id
                JOIN  t_artist_profile ap ON ap.c_artist_id   = a.c_artist_id
                JOIN  t_category      cat ON cat.c_category_id = a.c_category_id
                WHERE oi.c_order_id = @orderId
                ORDER BY oi.c_item_id";

                using (var cmd = new NpgsqlCommand(itemsSql, _conn))
                {
                    cmd.Parameters.AddWithValue("@orderId", orderId);

                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        items.Add(new t_OrderItem
                        {
                            ItemId          = r.GetInt32(0),
                            OrderId         = r.GetInt32(1),
                            ArtworkId       = r.GetInt32(2),
                            PriceAtPurchase = r.GetDecimal(3),
                            Title           = r.GetString(4),
                            Description     = r.IsDBNull(5) ? null : r.GetString(5),
                            PreviewPath     = r.IsDBNull(6) ? null : r.GetString(6),
                            CategoryName    = r.IsDBNull(7) ? null : r.GetString(7),
                            ArtistName      = r.GetString(8),
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetOrderDetailAsync Error: " + ex.Message);
            }
            finally
            {
                await _conn.CloseAsync();
            }

            if (order == null) return null;
            return new t_OrderHistory { Order = order, Items = items };
        }
    }
}