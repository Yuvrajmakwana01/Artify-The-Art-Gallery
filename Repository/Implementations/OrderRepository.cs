using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using Repository.Interfaces;
using Repository.Models;

namespace Repository.Implementations;

public class OrderRepository : IOrderInterface
{
    private readonly string _connectionString;
    private const int MAX_DOWNLOADS = 2;

    public OrderRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("pgconn");
    }

    // ── GET FULL ORDER DETAIL ─────────────────────────────────────────────
    public async Task<t_OrderDetail?> GetOrderDetailAsync(int orderId, int buyerId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var orderCmd = new NpgsqlCommand(@"
            SELECT
                o.c_order_id,
                o.c_created_at,
                o.c_order_status,
                o.c_total_amount,
                p.c_method,
                p.c_transaction_id,
                p.c_payment_status,
                p.c_commission_deducted,
                p.c_artist_payout_amount,
                u.c_full_name,
                u.c_email,
                u.c_mobile
            FROM t_order o
            LEFT JOIN t_payment p ON p.c_order_id = o.c_order_id
            LEFT JOIN t_user u ON u.c_user_id = o.c_buyer_id
            WHERE o.c_order_id = @orderId AND o.c_buyer_id = @buyerId", conn);

        orderCmd.Parameters.Add("@orderId", NpgsqlDbType.Integer).Value = orderId;
        orderCmd.Parameters.Add("@buyerId", NpgsqlDbType.Integer).Value = buyerId;

        await using var reader = await orderCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var detail = new t_OrderDetail
        {
            OrderId            = reader.GetInt32(0),
            OrderDate          = reader.GetDateTime(1),
            OrderStatus        = reader.GetString(2),
            TotalAmount        = reader.GetDecimal(3),
            PaymentMethod      = reader.IsDBNull(4) ? "" : reader.GetString(4),
            TransactionId      = reader.IsDBNull(5) ? null : reader.GetString(5),
            PaymentStatus      = reader.IsDBNull(6) ? "" : reader.GetString(6),
            CommissionDeducted = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
            ArtistPayout       = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
            BuyerName          = reader.IsDBNull(9) ? "" : reader.GetString(9),
            BuyerEmail         = reader.IsDBNull(10) ? "" : reader.GetString(10),
            BuyerPhone         = reader.IsDBNull(11) ? null : reader.GetString(11),
        };

        await reader.CloseAsync();

        // ── Items + download count ───────────────────────────────────────
        await using var itemCmd = new NpgsqlCommand(@"
            SELECT
                oi.c_artwork_id,
                aw.c_title,
                ap.c_artist_name,
                oi.c_price_at_purchase,
                aw.c_preview_path,
                aw.c_original_path,
                COUNT(dl.c_download_id)
            FROM t_order_item oi
            JOIN t_artwork aw ON aw.c_artwork_id = oi.c_artwork_id
            JOIN t_artist_profile ap ON ap.c_artist_id = aw.c_artist_id
            LEFT JOIN t_download_log dl
                ON dl.c_artwork_id = oi.c_artwork_id
                AND dl.c_buyer_id = @buyerId
            WHERE oi.c_order_id = @orderId
            GROUP BY
                oi.c_artwork_id, aw.c_title, ap.c_artist_name,
                oi.c_price_at_purchase, aw.c_preview_path, aw.c_original_path", conn);

        itemCmd.Parameters.Add("@orderId", NpgsqlDbType.Integer).Value = orderId;
        itemCmd.Parameters.Add("@buyerId", NpgsqlDbType.Integer).Value = buyerId;

        await using var ir = await itemCmd.ExecuteReaderAsync();

        while (await ir.ReadAsync())
        {
            int count = Convert.ToInt32(ir.GetInt64(6));

            detail.Items.Add(new t_OrderItemDetail
            {
                ArtworkId     = ir.GetInt32(0),
                Title         = ir.GetString(1),
                ArtistName    = ir.IsDBNull(2) ? "Unknown" : ir.GetString(2),
                Price         = ir.GetDecimal(3),
                PreviewPath   = ir.IsDBNull(4) ? null : ir.GetString(4),
                OriginalPath  = ir.IsDBNull(5) ? null : ir.GetString(5),
                DownloadCount = count,
                DownloadsLeft = Math.Max(0, MAX_DOWNLOADS - count),
                CanDownload   = count < MAX_DOWNLOADS
            });
        }

        return detail;
    }

    // ── LIST ORDERS ──────────────────────────────────────────────────────
    public async Task<List<t_OrderDetail>> GetOrdersByBuyerAsync(int buyerId)
    {
        var orders = new List<t_OrderDetail>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT
                o.c_order_id,
                o.c_created_at,
                o.c_order_status,
                o.c_total_amount,
                p.c_payment_status
            FROM t_order o
            LEFT JOIN t_payment p ON p.c_order_id = o.c_order_id
            WHERE o.c_buyer_id = @buyerId
            ORDER BY o.c_created_at DESC", conn);

        cmd.Parameters.Add("@buyerId", NpgsqlDbType.Integer).Value = buyerId;

        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            orders.Add(new t_OrderDetail
            {
                OrderId       = r.GetInt32(0),
                OrderDate     = r.GetDateTime(1),
                OrderStatus   = r.GetString(2),
                TotalAmount   = r.GetDecimal(3),
                PaymentStatus = r.IsDBNull(4) ? "" : r.GetString(4)
            });
        }

        return orders;
    }

    // ── GET DOWNLOAD COUNT ───────────────────────────────────────────────
    public async Task<int> GetDownloadCountAsync(int buyerId, int artworkId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT COUNT(c_download_id)
            FROM t_download_log
            WHERE c_buyer_id = @buyerId AND c_artwork_id = @artworkId", conn);

        cmd.Parameters.Add("@buyerId", NpgsqlDbType.Integer).Value = buyerId;
        cmd.Parameters.Add("@artworkId", NpgsqlDbType.Integer).Value = artworkId;

        return Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
    }

    // ── LOG DOWNLOAD ─────────────────────────────────────────────────────
    public async Task LogDownloadAsync(int orderId, int artworkId, int buyerId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO t_download_log (c_order_id, c_artwork_id, c_buyer_id)
            VALUES (@orderId, @artworkId, @buyerId)", conn);

        cmd.Parameters.Add("@orderId", NpgsqlDbType.Integer).Value = orderId;
        cmd.Parameters.Add("@artworkId", NpgsqlDbType.Integer).Value = artworkId;
        cmd.Parameters.Add("@buyerId", NpgsqlDbType.Integer).Value = buyerId;

        await cmd.ExecuteNonQueryAsync();
    }

    // ── VERIFY OWNERSHIP ─────────────────────────────────────────────────
    public async Task<bool> BuyerOwnsArtworkAsync(int buyerId, int artworkId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT COUNT(1)
            FROM t_order_item oi
            JOIN t_order o ON o.c_order_id = oi.c_order_id
            WHERE o.c_buyer_id = @buyerId
              AND oi.c_artwork_id = @artworkId
              AND o.c_order_status = 'Completed'", conn);

        cmd.Parameters.Add("@buyerId", NpgsqlDbType.Integer).Value = buyerId;
        cmd.Parameters.Add("@artworkId", NpgsqlDbType.Integer).Value = artworkId;

        return Convert.ToInt64(await cmd.ExecuteScalarAsync() ?? 0L) > 0;
    }

    // ── GET ORIGINAL PATH ────────────────────────────────────────────────
    public async Task<string?> GetOriginalPathAsync(int artworkId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT c_original_path FROM t_artwork
            WHERE c_artwork_id = @artworkId", conn);

        cmd.Parameters.Add("@artworkId", NpgsqlDbType.Integer).Value = artworkId;

        var result = await cmd.ExecuteScalarAsync();
        return result is DBNull || result is null ? null : result.ToString();
    }
}