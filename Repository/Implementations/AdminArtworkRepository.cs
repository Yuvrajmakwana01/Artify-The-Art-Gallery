using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Repository.Interfaces;
using Npgsql;
using Repository.Models;
using Repository.Services;
using Microsoft.Extensions.Logging;

namespace Repository.Implementations
{
    /// <summary>
    /// Raw ADO.NET: NpgsqlConnection → NpgsqlCommand → NpgsqlDataReader.
    /// No Dapper. No Entity Framework. No search — filter by status only.
    /// </summary>
    public class AdminArtworkRepository : IAdminArtworkInterface
    {
        private readonly NpgsqlConnection _conn;
        private readonly ElasticService _elasticService;
        private readonly ILogger<AdminArtworkRepository> _logger;

        public AdminArtworkRepository(
            NpgsqlConnection conn,
            ElasticService elasticService,
            ILogger<AdminArtworkRepository> logger)
        {
            _conn = conn;
            _elasticService = elasticService;
            _logger = logger;
        }

        private async Task EnsureOpenAsync()
        {
            if (_conn.State != ConnectionState.Open)
                await _conn.OpenAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        //  GET ARTWORKS — filter by status + pagination
        // ─────────────────────────────────────────────────────────────────

        public async Task<PagedResult<ArtworkModel>> GetArtworksAsync(
            string? status, int page, int pageSize)
        {
            await EnsureOpenAsync();

            const string normalizedStatusSql = "COALESCE(NULLIF(BTRIM(a.c_approval_status), ''), 'Pending')";
            bool hasStatus = !string.IsNullOrWhiteSpace(status)
                             && !status.Equals("All", StringComparison.OrdinalIgnoreCase);

            string whereClause = hasStatus ? $"WHERE {normalizedStatusSql} = @status" : string.Empty;
            string orderClause = hasStatus
                ? "ORDER BY a.c_created_at DESC"
                : $@"
            ORDER BY CASE {normalizedStatusSql}
                        WHEN 'Pending'  THEN 0
                        WHEN 'Approved' THEN 1
                        WHEN 'Rejected' THEN 2
                        ELSE 3
                     END,
                     a.c_created_at DESC";

            // COUNT for pagination total
            string countSql = $@"
            SELECT COUNT(*)
            FROM   t_artwork a
            JOIN   t_artist_profile ap ON ap.c_artist_id  = a.c_artist_id
            JOIN   t_category        c  ON c.c_category_id = a.c_category_id
            {whereClause}";

            await using var countCmd = new NpgsqlCommand(countSql, _conn);
            if (hasStatus) countCmd.Parameters.AddWithValue("@status", status!);

            int total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            // DATA with LIMIT + OFFSET
            string dataSql = $@"
            SELECT
                a.c_artwork_id,
                a.c_artist_id,
                ap.c_artist_name,
                a.c_category_id,
                c.c_category_name,
                a.c_title,
                a.c_description,
                a.c_price,
                a.c_preview_path,
                {normalizedStatusSql} AS c_approval_status,
                a.c_admin_note,
                a.c_created_at,
                a.c_likes_count
            FROM   t_artwork a
            JOIN   t_artist_profile ap ON ap.c_artist_id  = a.c_artist_id
            JOIN   t_category        c  ON c.c_category_id = a.c_category_id
            {whereClause}
            {orderClause}
            LIMIT  @pageSize
            OFFSET @offset";

            await using var dataCmd = new NpgsqlCommand(dataSql, _conn);
            if (hasStatus) dataCmd.Parameters.AddWithValue("@status", status!);
            dataCmd.Parameters.AddWithValue("@pageSize", pageSize);
            dataCmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);

            await using var reader = await dataCmd.ExecuteReaderAsync();

            var list = new List<ArtworkModel>();
            while (await reader.ReadAsync())
                list.Add(MapArtwork(reader));

            return new PagedResult<ArtworkModel>
            {
                c_Data = list,
                c_Total = total,
                c_Page = page,
                c_PageSize = pageSize
            };
        }

        // ─────────────────────────────────────────────────────────────────
        //  GET SINGLE ARTWORK BY ID
        // ─────────────────────────────────────────────────────────────────

        public async Task<ArtworkModel?> GetArtworkByIdAsync(int artworkId)
        {
            await EnsureOpenAsync();

            const string sql = @"
            SELECT
                a.c_artwork_id,
                a.c_artist_id,
                ap.c_artist_name,
                a.c_category_id,
                c.c_category_name,
                a.c_title,
                a.c_description,
                a.c_price,
                a.c_preview_path,
                COALESCE(NULLIF(BTRIM(a.c_approval_status), ''), 'Pending') AS c_approval_status,
                a.c_admin_note,
                a.c_created_at,
                a.c_likes_count
            FROM t_artwork a
            JOIN t_artist_profile ap ON ap.c_artist_id  = a.c_artist_id
            JOIN t_category        c  ON c.c_category_id = a.c_category_id
            WHERE a.c_artwork_id = @artworkId";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@artworkId", artworkId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
                return MapArtwork(reader);

            return null;
        }

        // ─────────────────────────────────────────────────────────────────
        //  UPDATE STATUS + ADMIN NOTE
        // ─────────────────────────────────────────────────────────────────

        public async Task UpdateArtworkStatusAsync(int artworkId, string status, string adminNote)
        {
            await EnsureOpenAsync();

            const string sql = @"
            UPDATE t_artwork
            SET    c_approval_status = @status,
                   c_admin_note      = @adminNote
            WHERE  c_artwork_id      = @artworkId";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.Parameters.AddWithValue("@adminNote", adminNote);
            cmd.Parameters.AddWithValue("@artworkId", artworkId);

            await cmd.ExecuteNonQueryAsync();

            if (status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
            {
                await IndexApprovedArtworkAsync(artworkId);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  RESET REJECTED COUNT
        // ─────────────────────────────────────────────────────────────────

        private async Task IndexApprovedArtworkAsync(int artworkId)
        {
            try
            {
                var artwork = await GetArtworkByIdAsync(artworkId);
                if (artwork is null ||
                    !artwork.c_ApprovalStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                await _elasticService.IndexArtworkAsync(new ArtworkSearchDocument
                {
                    Id = artwork.c_ArtworkId,
                    Title = artwork.c_Title,
                    Description = artwork.c_Description,
                    ArtistName = artwork.c_ArtistName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Artwork {ArtworkId} was approved in PostgreSQL but failed to index in Elasticsearch.", artworkId);
            }
        }

        public async Task ResetRejectedCountAsync(int artistId)
        {
            await EnsureOpenAsync();

            const string sql = @"
            UPDATE t_artist_profile
            SET    c_rejected_count = 0
            WHERE  c_artist_id = @artistId";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@artistId", artistId);

            await cmd.ExecuteNonQueryAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        //  INCREMENT REJECTED COUNT → return new value
        // ─────────────────────────────────────────────────────────────────

        public async Task<int> IncrementRejectedCountAsync(int artistId)
        {
            await EnsureOpenAsync();

            const string sql = @"
            UPDATE t_artist_profile
            SET    c_rejected_count = COALESCE(c_rejected_count, 0) + 1
            WHERE  c_artist_id = @artistId
            RETURNING c_rejected_count";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@artistId", artistId);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        // ─────────────────────────────────────────────────────────────────
        //  BLOCK ARTIST for 5 minutes
        // ─────────────────────────────────────────────────────────────────

        public async Task BlockArtistAsync(int artistId)
        {
            await EnsureOpenAsync();

            const string sql = @"
            UPDATE t_artist_profile
            SET    c_blocked_until = NOW() + INTERVAL '1 days'
            WHERE  c_artist_id = @artistId";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@artistId", artistId);

            await cmd.ExecuteNonQueryAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        //  IS ARTIST BLOCKED?
        // ─────────────────────────────────────────────────────────────────

        public async Task<bool> IsArtistBlockedAsync(int artistId)
        {
            await EnsureOpenAsync();

            const string sql = @"
            SELECT c_blocked_until
            FROM   t_artist_profile
            WHERE  c_artist_id = @artistId";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@artistId", artistId);

            var result = await cmd.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
                return false;

            return Convert.ToDateTime(result) > DateTime.UtcNow;
        }

        public async Task<string?> GetArtistEmailAsync(int artistId)
        {
            await EnsureOpenAsync();

            const string sql = @"
                SELECT c_artist_email
                FROM   t_artist_profile
                WHERE  c_artist_id = @artistId";

            await using var cmd = new NpgsqlCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@artistId", artistId);

            var result = await cmd.ExecuteScalarAsync();
            return result == DBNull.Value || result is null ? null : result.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        //  PRIVATE — map reader row → ArtworkModel
        // ─────────────────────────────────────────────────────────────────

        private static ArtworkModel MapArtwork(NpgsqlDataReader r)
        {
            return new ArtworkModel
            {
                c_ArtworkId = r.GetInt32(r.GetOrdinal("c_artwork_id")),
                c_ArtistId = r.GetInt32(r.GetOrdinal("c_artist_id")),
                c_ArtistName = r.GetString(r.GetOrdinal("c_artist_name")),
                c_CategoryId = r.GetInt32(r.GetOrdinal("c_category_id")),
                c_CategoryName = r.GetString(r.GetOrdinal("c_category_name")),
                c_Title = r.GetString(r.GetOrdinal("c_title")),
                c_Description = r.IsDBNull(r.GetOrdinal("c_description"))
                                    ? string.Empty
                                    : r.GetString(r.GetOrdinal("c_description")),
                c_Price = r.GetDecimal(r.GetOrdinal("c_price")),
                c_PreviewPath = r.IsDBNull(r.GetOrdinal("c_preview_path"))
                                    ? string.Empty
                                    : r.GetString(r.GetOrdinal("c_preview_path")),
                c_ApprovalStatus = r.GetString(r.GetOrdinal("c_approval_status")),
                c_AdminNote = r.IsDBNull(r.GetOrdinal("c_admin_note"))
                                    ? string.Empty
                                    : r.GetString(r.GetOrdinal("c_admin_note")),
                c_CreatedAt = r.GetDateTime(r.GetOrdinal("c_created_at")),
                c_LikesCount = r.GetInt32(r.GetOrdinal("c_likes_count"))
            };
        }
    }
}
