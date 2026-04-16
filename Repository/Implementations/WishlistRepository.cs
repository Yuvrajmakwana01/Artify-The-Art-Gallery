using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using Repository.Interfaces;
using Repository.Models;

public class WishlistRepository : IWishlistInterface
{
    private readonly string _connectionString;

    public WishlistRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("pgconn");
    }

    /// <summary>Get all wishlist items for a buyer with artwork + artist details.</summary>
    public async Task<List<t_Wishlist>> GetWishlistAsync(int buyerId)
    {
        var list = new List<t_Wishlist>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT
                w.c_wishlist_id,
                w.c_buyer_id,
                w.c_artwork_id,
                a.c_title            AS artwork_title,
                u.c_full_name        AS artist_name,
                a.c_price,
                a.c_preview_path,
                c.c_category_name,
                NOT EXISTS (
                    SELECT 1 FROM t_order_item oi
                    JOIN t_order o ON oi.c_order_id = o.c_order_id
                    WHERE oi.c_artwork_id = a.c_artwork_id
                      AND o.c_order_status = 'Completed'
                ) AS is_available
            FROM t_wishlist w
            JOIN t_artwork  a ON w.c_artwork_id  = a.c_artwork_id
            JOIN t_artist_profile ap ON a.c_artist_id = ap.c_artist_id
            JOIN t_user u ON ap.c_artist_id = u.c_user_id
            LEFT JOIN t_category c ON a.c_category_id = c.c_category_id
            WHERE w.c_buyer_id = @buyerId
            ORDER BY w.c_wishlist_id DESC", conn);

        cmd.Parameters.Add("@buyerId", NpgsqlDbType.Integer).Value = buyerId;

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new t_Wishlist
            {
                WishlistId   = reader.GetInt32(reader.GetOrdinal("c_wishlist_id")),
                BuyerId      = reader.GetInt32(reader.GetOrdinal("c_buyer_id")),
                ArtworkId    = reader.GetInt32(reader.GetOrdinal("c_artwork_id")),
                ArtworkTitle = reader.GetString(reader.GetOrdinal("artwork_title")),
                ArtistName   = reader.GetString(reader.GetOrdinal("artist_name")),
                Price        = reader.GetDecimal(reader.GetOrdinal("c_price")),
                PreviewPath  = reader.IsDBNull(reader.GetOrdinal("c_preview_path"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("c_preview_path")),
                CategoryName = reader.IsDBNull(reader.GetOrdinal("c_category_name"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("c_category_name")),
                IsAvailable  = reader.GetBoolean(reader.GetOrdinal("is_available"))
            });
        }

        return list;
    }

    /// <summary>Add artwork to wishlist</summary>
    public async Task<bool> AddToWishlistAsync(int buyerId, int artworkId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO t_wishlist (c_buyer_id, c_artwork_id)
            VALUES (@buyerId, @artworkId)
            ON CONFLICT ON CONSTRAINT unique_wishlist DO NOTHING", conn);

        cmd.Parameters.Add("@buyerId", NpgsqlDbType.Integer).Value = buyerId;
        cmd.Parameters.Add("@artworkId", NpgsqlDbType.Integer).Value = artworkId;

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    /// <summary>Remove specific artwork</summary>
    public async Task<bool> RemoveFromWishlistAsync(int buyerId, int artworkId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            DELETE FROM t_wishlist
            WHERE c_buyer_id = @buyerId AND c_artwork_id = @artworkId", conn);

        cmd.Parameters.Add("@buyerId", NpgsqlDbType.Integer).Value = buyerId;
        cmd.Parameters.Add("@artworkId", NpgsqlDbType.Integer).Value = artworkId;

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    /// <summary>Clear wishlist</summary>
    public async Task<bool> ClearWishlistAsync(int buyerId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "DELETE FROM t_wishlist WHERE c_buyer_id = @buyerId", conn);

        cmd.Parameters.Add("@buyerId", NpgsqlDbType.Integer).Value = buyerId;

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    /// <summary>Check if exists</summary>
    public async Task<bool> IsInWishlistAsync(int buyerId, int artworkId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT 1 FROM t_wishlist
            WHERE c_buyer_id = @buyerId AND c_artwork_id = @artworkId
            LIMIT 1", conn);

        cmd.Parameters.Add("@buyerId", NpgsqlDbType.Integer).Value = buyerId;
        cmd.Parameters.Add("@artworkId", NpgsqlDbType.Integer).Value = artworkId;

        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }
}