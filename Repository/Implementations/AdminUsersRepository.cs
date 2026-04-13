using Npgsql;
using NpgsqlTypes;
using Repository.Interfaces;
using Repository.Models;

namespace Repository.Implementations;

public class AdminUsersRepository : IAdminUsersInterface
{
    private readonly NpgsqlConnection _connection;

    public AdminUsersRepository(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<AdminUserDto>> GetUsersAsync(string? search)
    {
        await EnsureOpenAsync();

        var list = new List<AdminUserDto>();
        await using var cmd = new NpgsqlCommand(@"
            SELECT
                u.c_user_id,
                u.c_email,
                u.c_full_name,
                u.c_username,
                u.c_gender,
                u.c_mobile,
                u.c_profile_image,
                u.c_created_at,
                COALESCE(COUNT(o.c_order_id), 0)::int AS orders_count,
                COALESCE(SUM(o.c_total_amount), 0)::numeric(18,2) AS total_spend,
                CASE WHEN ap.c_artist_id IS NOT NULL THEN 'Artist' ELSE 'User' END AS role
            FROM t_user u
            LEFT JOIN t_order o ON o.c_buyer_id = u.c_user_id
            LEFT JOIN t_artist_profile ap ON ap.c_artist_id = u.c_user_id
            WHERE @search IS NULL OR @search = '' OR
                u.c_full_name ILIKE '%' || @search || '%' OR
                u.c_email ILIKE '%' || @search || '%' OR
                u.c_username ILIKE '%' || @search || '%'
            GROUP BY u.c_user_id, u.c_email, u.c_full_name, u.c_username, u.c_gender, u.c_mobile, u.c_profile_image, u.c_created_at, ap.c_artist_id
            ORDER BY u.c_user_id DESC;", _connection);

        cmd.Parameters.Add("search", NpgsqlDbType.Text).Value = (object?)search ?? DBNull.Value;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new AdminUserDto
            {
                UserId = reader.GetInt32(0),
                Email = reader.GetString(1),
                FullName = reader.GetString(2),
                Username = reader.GetString(3),
                Gender = reader.GetString(4),
                Mobile = reader.IsDBNull(5) ? null : reader.GetString(5),
                ProfileImage = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = reader.GetDateTime(7),
                OrdersCount = reader.GetInt32(8),
                TotalSpend = reader.GetDecimal(9),
                Role = reader.GetString(10)
            });
        }

        return list;
    }

    public async Task<AdminUserStatsDto> GetUserStatsAsync()
    {
        await EnsureOpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT
                (SELECT COUNT(*)::int FROM t_user) AS total_users,
                (SELECT COUNT(*)::int FROM t_order) AS total_orders,
                (SELECT COALESCE(SUM(c_total_amount), 0)::numeric(18,2) FROM t_order) AS total_spend;", _connection);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AdminUserStatsDto
            {
                TotalUsers = reader.GetInt32(0),
                TotalOrders = reader.GetInt32(1),
                TotalSpend = reader.GetDecimal(2)
            };
        }

        return new AdminUserStatsDto();
    }

    public async Task<AdminUserDto?> GetUserByIdAsync(int userId)
    {
        await EnsureOpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT
                u.c_user_id,
                u.c_email,
                u.c_full_name,
                u.c_username,
                u.c_gender,
                u.c_mobile,
                u.c_profile_image,
                u.c_created_at,
                COALESCE(COUNT(o.c_order_id), 0)::int AS orders_count,
                COALESCE(SUM(o.c_total_amount), 0)::numeric(18,2) AS total_spend,
                CASE WHEN ap.c_artist_id IS NOT NULL THEN 'Artist' ELSE 'User' END AS role
            FROM t_user u
            LEFT JOIN t_order o ON o.c_buyer_id = u.c_user_id
            LEFT JOIN t_artist_profile ap ON ap.c_artist_id = u.c_user_id
            WHERE u.c_user_id = @userId
            GROUP BY u.c_user_id, u.c_email, u.c_full_name, u.c_username, u.c_gender, u.c_mobile, u.c_profile_image, u.c_created_at, ap.c_artist_id;", _connection);

        cmd.Parameters.AddWithValue("userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AdminUserDto
            {
                UserId = reader.GetInt32(0),
                Email = reader.GetString(1),
                FullName = reader.GetString(2),
                Username = reader.GetString(3),
                Gender = reader.GetString(4),
                Mobile = reader.IsDBNull(5) ? null : reader.GetString(5),
                ProfileImage = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = reader.GetDateTime(7),
                OrdersCount = reader.GetInt32(8),
                TotalSpend = reader.GetDecimal(9),
                Role = reader.GetString(10)
            };
        }

        return null;
    }

    public async Task<bool> UpdateUserAsync(int userId, AdminUserUpdateRequest request)
    {
        await EnsureOpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            UPDATE t_user
            SET c_email = @email,
                c_full_name = @fullName,
                c_username = @username,
                c_gender = @gender,
                c_mobile = @mobile,
                c_profile_image = @profileImage
            WHERE c_user_id = @userId;", _connection);

        cmd.Parameters.AddWithValue("email", request.Email.Trim());
        cmd.Parameters.AddWithValue("fullName", request.FullName.Trim());
        cmd.Parameters.AddWithValue("username", request.Username.Trim());
        cmd.Parameters.AddWithValue("gender", request.Gender.Trim());
        cmd.Parameters.AddWithValue("mobile", (object?)request.Mobile ?? DBNull.Value);
        cmd.Parameters.AddWithValue("profileImage", (object?)request.ProfileImage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("userId", userId);

        var affected = await cmd.ExecuteNonQueryAsync();
        return affected > 0;
    }

    public async Task<bool> DeleteUserAsync(int userId)
    {
        await EnsureOpenAsync();

        await using var cmd = new NpgsqlCommand("DELETE FROM t_user WHERE c_user_id = @userId;", _connection);
        cmd.Parameters.AddWithValue("userId", userId);

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
}
