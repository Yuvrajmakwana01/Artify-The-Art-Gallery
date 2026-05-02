using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Repository.Interfaces;
using Repository.Models;
using Repository.Services;

namespace Repository.Implementations;

public class BuyerUiArtworkRepository : IBuyerUiArtworkInterface
{
    private readonly string _connectionString;
    private readonly ElasticService _elasticService;
    private readonly ILogger<BuyerUiArtworkRepository> _logger;

    public BuyerUiArtworkRepository(
        IConfiguration config,
        ElasticService elasticService,
        ILogger<BuyerUiArtworkRepository> logger)
    {
        _connectionString = config.GetConnectionString("pgconn")
            ?? throw new InvalidOperationException("pgconn connection string is missing.");
        _elasticService = elasticService;
        _logger = logger;
    }

    // ── SHARED SQL ────────────────────────────────────────────────────────
    private const string SelectColumns = @"
        SELECT
            a.c_artwork_id,
            a.c_artist_id,
            a.c_category_id,
            a.c_title,
            a.c_description,
            a.c_price,
            a.c_preview_path,
            a.c_original_path,
            a.c_approval_status,
            a.c_created_at,
            a.c_likes_count,
            a.c_sell_count,
            ap.c_artist_name,
            cat.c_category_name
        FROM t_artwork a
        JOIN t_artist_profile ap  ON ap.c_artist_id   = a.c_artist_id
        JOIN t_category       cat ON cat.c_category_id = a.c_category_id";

    // ── MAPPER ────────────────────────────────────────────────────────────
    private static t_BuyerUiArtwork MapRow(NpgsqlDataReader r) => new()
    {
        ArtworkId      = r.GetInt32(0),
        ArtistId       = r.GetInt32(1),
        CategoryId     = r.GetInt32(2),
        Title          = r.GetString(3),
        Description    = r.IsDBNull(4)  ? null : r.GetString(4),
        Price          = r.GetDecimal(5),
        PreviewPath    = r.IsDBNull(6)  ? null : r.GetString(6),
        OriginalPath   = r.IsDBNull(7)  ? null : r.GetString(7),
        ApprovalStatus = r.GetString(8),
        CreatedAt      = r.GetDateTime(9),
        LikesCount     = r.GetInt32(10),
        SellCount      = r.GetInt32(11),
        ArtistName     = r.GetString(12),
        CategoryName   = r.GetString(13),
    };

    // ── GET ALL APPROVED ──────────────────────────────────────────────────
    public async Task<List<t_BuyerUiArtwork>> GetAllApprovedAsync()
    {
        var list = new List<t_BuyerUiArtwork>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            SelectColumns + " WHERE a.c_approval_status = 'Approved' ORDER BY a.c_created_at DESC",
            conn);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            list.Add(MapRow(reader));

        return list;
    }

    // ── GET BY ID ─────────────────────────────────────────────────────────
    public async Task<List<t_BuyerUiArtwork>> SearchArtworks(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllApprovedAsync();

        try
        {
            var artworkIds = await _elasticService.SearchArtworkIdsAsync(query);
            return artworkIds.Count == 0
                ? new List<t_BuyerUiArtwork>()
                : await GetApprovedByIdsAsync(artworkIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Elasticsearch search failed. Falling back to PostgreSQL search for query '{Query}'.", query);
            return await SearchArtworksFromDbAsync(query);
        }
    }

    private async Task<List<t_BuyerUiArtwork>> GetApprovedByIdsAsync(List<int> artworkIds)
    {
        var list = new List<t_BuyerUiArtwork>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            SelectColumns + " WHERE a.c_approval_status = 'Approved' AND a.c_artwork_id = ANY(@ids)",
            conn);
        cmd.Parameters.Add("@ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = artworkIds.ToArray();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(MapRow(reader));

        var rank = artworkIds
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);

        return list
            .OrderBy(artwork => rank.TryGetValue(artwork.ArtworkId, out var index) ? index : int.MaxValue)
            .ToList();
    }

    private async Task<List<t_BuyerUiArtwork>> SearchArtworksFromDbAsync(string query)
    {
        var list = new List<t_BuyerUiArtwork>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            SelectColumns + @"
            WHERE a.c_approval_status = 'Approved'
              AND (
                  a.c_title ILIKE @query
                  OR COALESCE(a.c_description, '') ILIKE @query
                  OR ap.c_artist_name ILIKE @query
              )
            ORDER BY a.c_created_at DESC",
            conn);
        cmd.Parameters.Add("@query", NpgsqlDbType.Text).Value = $"%{query.Trim()}%";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(MapRow(reader));

        return list;
    }

    public async Task<t_BuyerUiArtwork?> GetByIdAsync(int artworkId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            SelectColumns + " WHERE a.c_artwork_id = @id AND a.c_approval_status = 'Approved'",
            conn);

        cmd.Parameters.Add("@id", NpgsqlDbType.Integer).Value = artworkId;

        await using var reader = await cmd.ExecuteReaderAsync();

        return await reader.ReadAsync() ? MapRow(reader) : null;
    }
}
