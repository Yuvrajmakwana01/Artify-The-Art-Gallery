using Npgsql;
using NpgsqlTypes;
using Repository.Interfaces;
using Repository.Models;

namespace Repository.Implementations;

public class AdminCategoriesRepository : IAdmincategoiresInteface
{
    private const string DuplicateCategoryMessage = "Category already exists. Please use a different category name.";
    private readonly NpgsqlConnection _connection;
    private bool _categorySchemaEnsured;

    public AdminCategoriesRepository(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<AdminCategoryDto>> GetCategoriesAsync(string? search, string? status)
    {
        await EnsureOpenAsync();
        await EnsureCategorySchemaAsync();

        var list = new List<AdminCategoryDto>();
        await using var cmd = new NpgsqlCommand(@"
            SELECT
                c.c_category_id,
                c.c_icon,
                c.c_category_name,
                c.c_category_description,
                CASE
                    WHEN LOWER(REPLACE(c.c_is_active::text, ' ', '_')) IN ('true', 't', '1', 'yes', 'y', 'active') THEN 'Active'
                    WHEN LOWER(REPLACE(c.c_is_active::text, ' ', '_')) IN ('under_review', 'underreview', 'review', 'pending') THEN 'Under_Review'
                    ELSE 'Inactive'
                END AS status,
                CASE
                    WHEN LOWER(REPLACE(c.c_is_active::text, ' ', '_')) IN ('true', 't', '1', 'yes', 'y', 'active') THEN TRUE
                    ELSE FALSE
                END AS is_active,
                c.c_created_at,
                COUNT(a.c_artwork_id)::int AS artwork_count
            FROM t_category c
            LEFT JOIN t_artwork a ON a.c_category_id = c.c_category_id
            WHERE
                (@search IS NULL OR @search = '' OR
                    c.c_category_name ILIKE '%' || @search || '%' OR
                    COALESCE(c.c_category_description, '') ILIKE '%' || @search || '%')
                AND (
                    @status IS NULL OR @status = '' OR
                    (LOWER(REPLACE(@status, ' ', '_')) = 'active' AND LOWER(REPLACE(c.c_is_active::text, ' ', '_')) IN ('true', 't', '1', 'yes', 'y', 'active')) OR
                    (LOWER(REPLACE(@status, ' ', '_')) = 'under_review' AND LOWER(REPLACE(c.c_is_active::text, ' ', '_')) IN ('under_review', 'underreview', 'review', 'pending')) OR
                    (LOWER(REPLACE(@status, ' ', '_')) = 'inactive' AND LOWER(REPLACE(c.c_is_active::text, ' ', '_')) IN ('false', 'f', '0', 'no', 'n', 'inactive'))
                )
            GROUP BY c.c_category_id, c.c_category_name, c.c_category_description, c.c_is_active, c.c_created_at
            ORDER BY c.c_category_id DESC;", _connection);

        cmd.Parameters.Add("search", NpgsqlDbType.Text).Value = (object?)search ?? DBNull.Value;
        cmd.Parameters.Add("status", NpgsqlDbType.Text).Value = (object?)status ?? DBNull.Value;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new AdminCategoryDto
            {
                CategoryId = reader.GetInt32(0),
                CategoryIcon = reader.IsDBNull(1) ? "🎨" : reader.GetString(1),
                CategoryName = reader.GetString(2),
                CategoryDescription = reader.IsDBNull(3) ? null : reader.GetString(3),
                Status = reader.GetString(4),
                IsActive = reader.GetBoolean(5),
                CreatedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                ArtworkCount = reader.GetInt32(7)
            });
        }

        return list;
    }

    public async Task<AdminCategoryStatsDto> GetCategoryStatsAsync()
    {
        await EnsureOpenAsync();
        await EnsureCategorySchemaAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT
                COUNT(*)::int AS total,
                COUNT(*) FILTER (WHERE LOWER(REPLACE(c_is_active::text, ' ', '_')) IN ('true', 't', '1', 'yes', 'y', 'active'))::int AS active,
                COUNT(*) FILTER (WHERE LOWER(REPLACE(c_is_active::text, ' ', '_')) IN ('false', 'f', '0', 'no', 'n', 'inactive'))::int AS inactive,
                COUNT(*) FILTER (WHERE LOWER(REPLACE(c_is_active::text, ' ', '_')) IN ('under_review', 'underreview', 'review', 'pending'))::int AS under_review,
                COALESCE((SELECT SUM(c_sell_count)::int FROM t_artwork), 0) AS total_artworks
            FROM t_category;", _connection);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AdminCategoryStatsDto
            {
                Total = reader.GetInt32(0),
                Active = reader.GetInt32(1),
                Inactive = reader.GetInt32(2),
                UnderReview = reader.GetInt32(3),
                TotalArtworks = reader.GetInt32(4)
            };
        }

        return new AdminCategoryStatsDto();
    }

    public async Task<AdminCategoryDto?> GetCategoryByIdAsync(int categoryId)
    {
        await EnsureOpenAsync();
        await EnsureCategorySchemaAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT
                c.c_category_id,
                c.c_icon,
                c.c_category_name,
                c.c_category_description,
                CASE
                    WHEN LOWER(REPLACE(c.c_is_active::text, ' ', '_')) IN ('true', 't', '1', 'yes', 'y', 'active') THEN 'Active'
                    WHEN LOWER(REPLACE(c.c_is_active::text, ' ', '_')) IN ('under_review', 'underreview', 'review', 'pending') THEN 'Under_Review'
                    ELSE 'Inactive'
                END AS status,
                CASE
                    WHEN LOWER(REPLACE(c.c_is_active::text, ' ', '_')) IN ('true', 't', '1', 'yes', 'y', 'active') THEN TRUE
                    ELSE FALSE
                END AS is_active,
                c.c_created_at,
                COUNT(a.c_artwork_id)::int AS artwork_count
            FROM t_category c
            LEFT JOIN t_artwork a ON a.c_category_id = c.c_category_id
            WHERE c.c_category_id = @categoryId
            GROUP BY c.c_category_id, c.c_category_name, c.c_category_description, c.c_is_active, c.c_created_at;", _connection);

        cmd.Parameters.AddWithValue("categoryId", categoryId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AdminCategoryDto
            {
                CategoryId = reader.GetInt32(0),
                CategoryIcon = reader.IsDBNull(1) ? "🎨" : reader.GetString(1),
                CategoryName = reader.GetString(2),
                CategoryDescription = reader.IsDBNull(3) ? null : reader.GetString(3),
                Status = reader.GetString(4),
                IsActive = reader.GetBoolean(5),
                CreatedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                ArtworkCount = reader.GetInt32(7)
            };
        }

        return null;
    }


    public async Task<int> AddCategoryAsync(AdminCategoryUpsertRequest request)
    {
        await EnsureOpenAsync();
        await EnsureCategorySchemaAsync();
        var normalizedName = request.CategoryName.Trim();
        if (await CategoryNameExistsAsync(normalizedName))
        {
            throw new InvalidOperationException(DuplicateCategoryMessage);
        }

        var status = ResolveCategoryStatus(request);

        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO t_category (c_icon, c_category_name, c_category_description, c_is_active, c_created_at)
            VALUES (@icon, @name, @description, @status::category_status, CURRENT_TIMESTAMP)
            RETURNING c_category_id;", _connection);

        cmd.Parameters.AddWithValue("icon", string.IsNullOrWhiteSpace(request.CategoryIcon) ? "🎨" : request.CategoryIcon.Trim());
        cmd.Parameters.AddWithValue("name", normalizedName);
        cmd.Parameters.AddWithValue("description", (object?)request.CategoryDescription ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", status);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<bool> UpdateCategoryAsync(int categoryId, AdminCategoryUpsertRequest request)
    {
        await EnsureOpenAsync();
        await EnsureCategorySchemaAsync();
        var normalizedName = request.CategoryName.Trim();
        if (await CategoryNameExistsAsync(normalizedName, categoryId))
        {
            throw new InvalidOperationException(DuplicateCategoryMessage);
        }

        var status = ResolveCategoryStatus(request);

        await using var cmd = new NpgsqlCommand(@"
            UPDATE t_category
            SET c_icon = @icon,
                c_category_name = @name,
                c_category_description = @description,
                c_is_active = @status::category_status
            WHERE c_category_id = @categoryId;", _connection);

        cmd.Parameters.AddWithValue("icon", string.IsNullOrWhiteSpace(request.CategoryIcon) ? "🎨" : request.CategoryIcon.Trim());
        cmd.Parameters.AddWithValue("name", normalizedName);
        cmd.Parameters.AddWithValue("description", (object?)request.CategoryDescription ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("categoryId", categoryId);

        var affected = await cmd.ExecuteNonQueryAsync();
        return affected > 0;
    }

    public async Task<bool> DeleteCategoryAsync(int categoryId)
    {
        await EnsureOpenAsync();
        await EnsureCategorySchemaAsync();

        await using var cmd = new NpgsqlCommand("DELETE FROM t_category WHERE c_category_id = @categoryId;", _connection);
        cmd.Parameters.AddWithValue("categoryId", categoryId);

        var affected = await cmd.ExecuteNonQueryAsync();
        return affected > 0;
    }

    private async Task EnsureOpenAsync()
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync();
        }
    }

    private async Task<bool> CategoryNameExistsAsync(string categoryName, int? excludeCategoryId = null)
    {
        await using var cmd = new NpgsqlCommand(@"
            SELECT 1
            FROM t_category
            WHERE LOWER(TRIM(c_category_name)) = LOWER(TRIM(@name))
              AND (@excludeCategoryId IS NULL OR c_category_id <> @excludeCategoryId)
            LIMIT 1;", _connection);

        cmd.Parameters.AddWithValue("name", categoryName);
        cmd.Parameters.Add("excludeCategoryId", NpgsqlDbType.Integer).Value = excludeCategoryId.HasValue
            ? excludeCategoryId.Value
            : DBNull.Value;

        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }

    private static string ResolveCategoryStatus(AdminCategoryUpsertRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            return NormalizeCategoryStatus(request.Status);
        }

        return request.IsActive ? "Active" : "Inactive";
    }

    private static string NormalizeCategoryStatus(string? status)
    {
        var normalized = (status ?? "").Trim().Replace(" ", "_");
        if (normalized.Equals("Active", StringComparison.OrdinalIgnoreCase)) return "Active";
        if (normalized.Equals("Under_Review", StringComparison.OrdinalIgnoreCase)) return "Under_Review";
        return "Inactive";
    }

    private async Task EnsureCategorySchemaAsync()
    {
        if (_categorySchemaEnsured)
        {
            return;
        }

        await using var cmd = new NpgsqlCommand(@"
            ALTER TABLE t_category
            ADD COLUMN IF NOT EXISTS c_created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP;

            ALTER TABLE t_category
            ADD COLUMN IF NOT EXISTS c_icon VARCHAR(16) DEFAULT '🎨';

            UPDATE t_category
            SET c_created_at = CURRENT_TIMESTAMP
            WHERE c_created_at IS NULL;", _connection);

        await cmd.ExecuteNonQueryAsync();
        _categorySchemaEnsured = true;
    }
}
