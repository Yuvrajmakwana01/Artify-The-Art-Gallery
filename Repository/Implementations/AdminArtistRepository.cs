using Npgsql;
using NpgsqlTypes;
using Repository.Interfaces;
using Repository.Models;

namespace Repository.Implementations;

public class AdminArtistRepository : IAdminArtistInterface
{
    private readonly NpgsqlConnection _connection;

    public AdminArtistRepository(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<AdminArtistDto>> GetArtistsAsync(string? search, string? status)
    {
        await EnsureOpenAsync();

        var list = new List<AdminArtistDto>();
        await using var cmd = new NpgsqlCommand(@"
            SELECT
                ap.c_artist_id,
                ap.c_artist_name,
                ap.c_artist_email,
                ap.c_biography,
                ap.c_cover_image,
                ap.c_rating_avg,
                ap.c_is_verified,
                COALESCE(ap.c_is_active, FALSE) AS is_active,
                COALESCE(ap.c_rejected_count, 0) AS rejected_count,
                COALESCE(COUNT(DISTINCT aw.c_artwork_id), 0)::int AS artworks_count,
                COALESCE(SUM(oi.c_price_at_purchase), 0)::numeric(18,2) AS total_sales,
                COALESCE(ap.c_created_at, u.c_created_at) AS created_at,
                CASE
                    WHEN COALESCE(ap.c_is_active, FALSE) = TRUE THEN 'Active'
                    ELSE 'Inactive'
                END AS status
            FROM t_artist_profile ap
            LEFT JOIN t_user u ON u.c_user_id = ap.c_artist_id
            LEFT JOIN t_artwork aw ON aw.c_artist_id = ap.c_artist_id
            LEFT JOIN t_order_item oi ON oi.c_artwork_id = aw.c_artwork_id
            WHERE
                (@search IS NULL OR @search = '' OR
                    ap.c_artist_name ILIKE '%' || @search || '%' OR
                    ap.c_artist_email ILIKE '%' || @search || '%')
                AND (
                    @status IS NULL OR @status = '' OR
                    (@status = 'Active' AND COALESCE(ap.c_is_active, FALSE) = TRUE) OR
                    (@status = 'Inactive' AND COALESCE(ap.c_is_active, FALSE) = FALSE)
                )
            GROUP BY
                ap.c_artist_id, ap.c_artist_name, ap.c_artist_email, ap.c_biography,
                ap.c_cover_image, ap.c_rating_avg, ap.c_is_verified, ap.c_is_active, ap.c_rejected_count,
                ap.c_created_at, u.c_created_at
            ORDER BY ap.c_artist_id DESC;", _connection);

        cmd.Parameters.Add("search", NpgsqlDbType.Text).Value = (object?)search ?? DBNull.Value;
        cmd.Parameters.Add("status", NpgsqlDbType.Text).Value = (object?)status ?? DBNull.Value;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new AdminArtistDto
            {
                ArtistId = reader.GetInt32(0),
                ArtistName = reader.GetString(1),
                ArtistEmail = reader.GetString(2),
                Biography = reader.IsDBNull(3) ? null : reader.GetString(3),
                CoverImage = reader.IsDBNull(4) ? null : reader.GetString(4),
                RatingAvg = reader.GetDecimal(5),
                IsVerified = reader.GetBoolean(6),
                RejectedCount = reader.GetInt32(8),
                ArtworksCount = reader.GetInt32(9),
                TotalSales = reader.GetDecimal(10),
                CreatedAt = reader.GetDateTime(11),
                Status = reader.GetString(12)
            });
        }

        return list;
    }

    public async Task<AdminArtistStatsDto> GetArtistStatsAsync()
    {
        await EnsureOpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT
                COUNT(*)::int AS total_artists,
                COUNT(*) FILTER (WHERE COALESCE(c_is_active, FALSE) = TRUE)::int AS active,
                COUNT(*) FILTER (WHERE COALESCE(c_is_active, FALSE) = FALSE)::int AS inactive,
                0::int AS under_review,
                COALESCE((
                    SELECT SUM(oi.c_price_at_purchase)
                    FROM t_order_item oi
                    INNER JOIN t_artwork aw ON aw.c_artwork_id = oi.c_artwork_id
                ), 0)::numeric(18,2) AS total_sales
            FROM t_artist_profile;", _connection);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AdminArtistStatsDto
            {
                TotalArtists = reader.GetInt32(0),
                Active = reader.GetInt32(1),
                Inactive = reader.GetInt32(2),
                UnderReview = reader.GetInt32(3),
                TotalSales = reader.GetDecimal(4)
            };
        }

        return new AdminArtistStatsDto();
    }

    public async Task<AdminArtistDto?> GetArtistByIdAsync(int artistId)
    {
        var rows = await GetArtistsAsync(null, null);
        return rows.FirstOrDefault(a => a.ArtistId == artistId);
    }

    public async Task<int> AddArtistAsync(AdminArtistUpsertRequest request)
    {
        await EnsureOpenAsync();

        await using var tx = await _connection.BeginTransactionAsync();
        try
        {
            var resolvedFullName = string.IsNullOrWhiteSpace(request.FullName) ? request.ArtistName : request.FullName.Trim();
            var resolvedUsername = string.IsNullOrWhiteSpace(request.Username)
                ? await GenerateUniqueUsernameAsync(request.ArtistName, tx)
                : request.Username!.Trim();

            var email = request.ArtistEmail.Trim();

            await using var userCmd = new NpgsqlCommand(@"
                INSERT INTO t_user
                (c_email, c_password_hash, c_full_name, c_username, c_gender, c_mobile, c_profile_image)
                VALUES
                (@email, @passwordHash, @fullName, @username, @gender, @mobile, @profileImage)
                RETURNING c_user_id;", _connection, tx);

            userCmd.Parameters.AddWithValue("email", email);
            userCmd.Parameters.AddWithValue("passwordHash", request.UserPasswordHash);
            userCmd.Parameters.AddWithValue("fullName", resolvedFullName);
            userCmd.Parameters.AddWithValue("username", resolvedUsername);
            userCmd.Parameters.AddWithValue("gender", request.Gender);
            userCmd.Parameters.AddWithValue("mobile", (object?)request.Mobile ?? DBNull.Value);
            userCmd.Parameters.AddWithValue("profileImage", (object?)request.ProfileImage ?? DBNull.Value);

            var userId = Convert.ToInt32(await userCmd.ExecuteScalarAsync());

            var (isVerified, isActive, rejectedCount) = MapStatusToArtistFlags(request.Status);

            await using var artistCmd = new NpgsqlCommand(@"
                INSERT INTO t_artist_profile
                (c_artist_id, c_artist_name, c_artist_email, c_password, c_biography, c_cover_image, c_rating_avg, c_is_verified, c_is_active, c_url, c_rejected_count)
                VALUES
                (@artistId, @artistName, @artistEmail, @password, @biography, @coverImage, 0.00, @isVerified, @isActive, NULL, @rejectedCount);", _connection, tx);

            artistCmd.Parameters.AddWithValue("artistId", userId);
            artistCmd.Parameters.AddWithValue("artistName", request.ArtistName.Trim());
            artistCmd.Parameters.AddWithValue("artistEmail", email);
            artistCmd.Parameters.AddWithValue("password", request.ArtistPassword);
            artistCmd.Parameters.AddWithValue("biography", (object?)request.Biography ?? DBNull.Value);
            artistCmd.Parameters.AddWithValue("coverImage", (object?)request.CoverImage ?? DBNull.Value);
            artistCmd.Parameters.AddWithValue("isVerified", isVerified);
            artistCmd.Parameters.AddWithValue("isActive", isActive);
            artistCmd.Parameters.AddWithValue("rejectedCount", rejectedCount);

            await artistCmd.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            return userId;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> UpdateArtistAsync(int artistId, AdminArtistUpsertRequest request)
    {
        await EnsureOpenAsync();

        await using var tx = await _connection.BeginTransactionAsync();
        try
        {
            var (isVerified, isActive, rejectedCount) = MapStatusToArtistFlags(request.Status);
            var email = request.ArtistEmail.Trim();
            string? currentArtistEmail = null;

            await using (var currentArtistCmd = new NpgsqlCommand(@"
                SELECT c_artist_email
                FROM t_artist_profile
                WHERE c_artist_id = @artistId;", _connection, tx))
            {
                currentArtistCmd.Parameters.AddWithValue("artistId", artistId);
                currentArtistEmail = (await currentArtistCmd.ExecuteScalarAsync()) as string;
            }

            await using var artistCmd = new NpgsqlCommand(@"
                UPDATE t_artist_profile
                SET c_artist_name = @artistName,
                    c_artist_email = @artistEmail,
                    c_biography = @biography,
                    c_cover_image = @coverImage,
                    c_is_verified = @isVerified,
                    c_is_active = @isActive,
                    c_rejected_count = @rejectedCount
                WHERE c_artist_id = @artistId;", _connection, tx);

            artistCmd.Parameters.AddWithValue("artistName", request.ArtistName.Trim());
            artistCmd.Parameters.AddWithValue("artistEmail", email);
            artistCmd.Parameters.AddWithValue("biography", (object?)request.Biography ?? DBNull.Value);
            artistCmd.Parameters.AddWithValue("coverImage", (object?)request.CoverImage ?? DBNull.Value);
            artistCmd.Parameters.AddWithValue("isVerified", isVerified);
            artistCmd.Parameters.AddWithValue("isActive", isActive);
            artistCmd.Parameters.AddWithValue("rejectedCount", rejectedCount);
            artistCmd.Parameters.AddWithValue("artistId", artistId);

            var artistAffected = await artistCmd.ExecuteNonQueryAsync();
            if (artistAffected == 0)
            {
                await tx.RollbackAsync();
                return false;
            }

            await using var userCmd = new NpgsqlCommand(@"
                UPDATE t_user
                SET c_email = @email,
                    c_full_name = @fullName,
                    c_mobile = @mobile,
                    c_profile_image = @profileImage,
                    c_gender = @gender
                WHERE c_user_id = @artistId
                  AND LOWER(c_email) = LOWER(@currentArtistEmail);", _connection, tx);

            var resolvedFullName = string.IsNullOrWhiteSpace(request.FullName) ? request.ArtistName : request.FullName.Trim();
            userCmd.Parameters.AddWithValue("email", email);
            userCmd.Parameters.AddWithValue("fullName", resolvedFullName);
            userCmd.Parameters.AddWithValue("mobile", (object?)request.Mobile ?? DBNull.Value);
            userCmd.Parameters.AddWithValue("profileImage", (object?)request.ProfileImage ?? DBNull.Value);
            userCmd.Parameters.AddWithValue("gender", request.Gender);
            userCmd.Parameters.AddWithValue("artistId", artistId);
            userCmd.Parameters.AddWithValue("currentArtistEmail", (object?)currentArtistEmail ?? DBNull.Value);

            await userCmd.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DeleteArtistAsync(int artistId)
    {
        await EnsureOpenAsync();

        await using var cmd = new NpgsqlCommand("DELETE FROM t_artist_profile WHERE c_artist_id = @artistId;", _connection);
        cmd.Parameters.AddWithValue("artistId", artistId);

        var affected = await cmd.ExecuteNonQueryAsync();
        return affected > 0;
    }

    private static (bool isVerified, bool isActive, int rejectedCount) MapStatusToArtistFlags(string? status)
    {
        return (status ?? "").Trim() switch
        {
            "Active" => (true, true, 0),
            "Inactive" => (false, false, 1),
            _ => (false, false, 0)
        };
    }

    private async Task<string> GenerateUniqueUsernameAsync(string source, NpgsqlTransaction tx)
    {
        var baseName = string.Concat((source ?? "artist").ToLowerInvariant().Where(char.IsLetterOrDigit));
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "artist";

        var candidate = baseName;
        var index = 1;

        while (true)
        {
            await using var checkCmd = new NpgsqlCommand("SELECT 1 FROM t_user WHERE c_username = @username LIMIT 1;", _connection, tx);
            checkCmd.Parameters.AddWithValue("username", candidate);
            var exists = await checkCmd.ExecuteScalarAsync();

            if (exists is null)
            {
                return candidate;
            }

            index++;
            candidate = $"{baseName}{index}";
        }
    }

    private async Task EnsureOpenAsync()
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync();
        }
    }
}
