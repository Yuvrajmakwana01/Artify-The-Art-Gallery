using System.Text.Json;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;

namespace Repository;

public class AdminRepository : IAdminInterface
{
    private readonly NpgsqlConnection _conn;
    private readonly IDistributedCache _cache;
    public AdminRepository(NpgsqlConnection conn, IDistributedCache cache)
    {
        _cache = cache;
        _conn = conn;
    }
    public async Task<AdminDashboard> GetAllDashboardInfo()
    {
        var cacheKey = "dashboard";

        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            Console.WriteLine("✅ Data coming from Redis cache");
            return JsonSerializer.Deserialize<AdminDashboard>(cached);
        }

        Console.WriteLine("❌ Data coming from Database");

        try
        {
            await _conn.OpenAsync();

            var query = @"
            SELECT 
                (SELECT COUNT(*) FROM t_user) AS TotalUsers,
                (SELECT COUNT(*) FROM t_artist_profile) AS TotalArtists,
                (SELECT COUNT(*) FROM t_artwork) AS TotalArts,
                (SELECT COUNT(*) FROM t_order) AS TotalSales,
                (SELECT COALESCE(SUM(c_total_amount),0) FROM t_order) AS TotalRevenue;
        ";

            using var cmd = new NpgsqlCommand(query, _conn);
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var result = new AdminDashboard
                {
                    c_TotalUsers = reader.GetInt32(0),
                    c_TotalArtists = reader.GetInt32(1),
                    c_TotalArts = reader.GetInt32(2),
                    c_TotalSales = reader.GetInt32(3),
                    c_TotalRevenue = reader.GetDecimal(4)
                };

                await _cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(result),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                    });

                return result;
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
        finally
        {
            await _conn.CloseAsync();
        }
    }
    public async Task<List<t_RevenueDto>> GetRevenue(string type)
    {
        List<t_RevenueDto> list = new();

        try
        {
            await _conn.OpenAsync();
            string query = "";

            if (type == "WEEKLY")
            {
                query = @"
                SELECT 
                    wd.day::date AS Period,
                    TO_CHAR(wd.day, 'Dy') AS Label,
                    COALESCE(SUM(p.c_amount_paid), 0),
                    COALESCE(SUM(p.c_commission_deducted), 0),
                    COALESCE(SUM(p.c_artist_payout_amount), 0)
                FROM generate_series(
                    DATE_TRUNC('week', CURRENT_DATE),
                    DATE_TRUNC('week', CURRENT_DATE) + INTERVAL '6 days',
                    INTERVAL '1 day'
                ) wd(day)
                LEFT JOIN t_payment p 
                    ON DATE(p.c_paid_at) = wd.day
                    AND LOWER(p.c_payment_status) = 'success'
                GROUP BY wd.day
                ORDER BY wd.day;";
            }
            else if (type == "MONTHLY")
            {
                query = @"
                SELECT 
                    m.month::date AS Period,
                    TO_CHAR(m.month, 'Mon') AS Label,
                    COALESCE(SUM(p.c_amount_paid), 0),
                    COALESCE(SUM(p.c_commission_deducted), 0),
                    COALESCE(SUM(p.c_artist_payout_amount), 0)
                FROM generate_series(
                    DATE_TRUNC('year', CURRENT_DATE),
                    DATE_TRUNC('year', CURRENT_DATE) + INTERVAL '11 months',
                    INTERVAL '1 month'
                ) m(month)
                LEFT JOIN t_payment p 
                    ON DATE_TRUNC('month', p.c_paid_at) = m.month
                    AND LOWER(p.c_payment_status) = 'success'
                GROUP BY m.month
                ORDER BY m.month;";
            }
            else if (type == "YEARLY")
            {
                query = @"
                SELECT 
                    y.year::date AS Period,
                    TO_CHAR(y.year, 'YYYY') AS Label,
                    COALESCE(SUM(p.c_amount_paid), 0),
                    COALESCE(SUM(p.c_commission_deducted), 0),
                    COALESCE(SUM(p.c_artist_payout_amount), 0)
                FROM generate_series(
                    DATE_TRUNC('year', CURRENT_DATE) - INTERVAL '4 years',
                    DATE_TRUNC('year', CURRENT_DATE),
                    INTERVAL '1 year'
                ) y(year)
                LEFT JOIN t_payment p 
                    ON DATE_TRUNC('year', p.c_paid_at) = y.year
                    AND LOWER(p.c_payment_status) = 'success'
                GROUP BY y.year
                ORDER BY y.year;";
            }

            using var cmd = new NpgsqlCommand(query, _conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new t_RevenueDto
                {
                    Period = DateOnly.FromDateTime(reader.GetDateTime(0)),
                    Label = reader.GetString(1),
                    GrossRevenue = reader.GetDecimal(2),
                    PlatformRevenue = reader.GetDecimal(3),
                    ArtistRevenue = reader.GetDecimal(4)
                });
            }
        }
        finally
        {
            await _conn.CloseAsync();
        }

        return list;
    }
    public async Task<List<t_UsersCount>> GetUsersCount(string type)
    {
        List<t_UsersCount> list = new();

        try
        {
            await _conn.OpenAsync();

            var query = @"
        SELECT 
            wd.day::date AS Period,
            TO_CHAR(wd.day, 'Dy') AS Label,
            COALESCE(u.UserCount, 0) AS UserCount,
            COALESCE(a.ArtistCount, 0) AS ArtistCount

        FROM generate_series(
            DATE_TRUNC('week', CURRENT_DATE),
            DATE_TRUNC('week', CURRENT_DATE) + INTERVAL '6 days',
            INTERVAL '1 day'
        ) AS wd(day)

        LEFT JOIN (
            SELECT DATE(c_created_at) AS day, COUNT(*) AS UserCount
            FROM t_user
            GROUP BY DATE(c_created_at)
        ) u ON u.day = wd.day::date

        LEFT JOIN (
            SELECT DATE(c_created_at) AS day, COUNT(*) AS ArtistCount
            FROM t_artist_profile
            GROUP BY DATE(c_created_at)
        ) a ON a.day = wd.day::date

        WHERE @type = 'WEEKLY'

        UNION ALL

        SELECT 
            md.month::date AS Period,
            TO_CHAR(md.month, 'Mon YYYY') AS Label,
            COALESCE(u.UserCount, 0),
            COALESCE(a.ArtistCount, 0)

        FROM generate_series(
            DATE_TRUNC('year', CURRENT_DATE),
            DATE_TRUNC('year', CURRENT_DATE) + INTERVAL '11 months',
            INTERVAL '1 month'
        ) AS md(month)

        LEFT JOIN (
            SELECT DATE_TRUNC('month', c_created_at) AS month, COUNT(*) AS UserCount
            FROM t_user
            GROUP BY 1
        ) u ON u.month = md.month

        LEFT JOIN (
            SELECT DATE_TRUNC('month', c_created_at) AS month, COUNT(*) AS ArtistCount
            FROM t_artist_profile
            GROUP BY 1
        ) a ON a.month = md.month

        WHERE @type = 'MONTHLY'

        UNION ALL

        SELECT 
            yd.year::date AS Period,
            TO_CHAR(yd.year, 'YYYY') AS Label,
            COALESCE(u.UserCount, 0),
            COALESCE(a.ArtistCount, 0)

        FROM generate_series(
            DATE_TRUNC('year', CURRENT_DATE) - INTERVAL '4 years',
            DATE_TRUNC('year', CURRENT_DATE),
            INTERVAL '1 year'
        ) AS yd(year)

        LEFT JOIN (
            SELECT DATE_TRUNC('year', c_created_at) AS year, COUNT(*) AS UserCount
            FROM t_user
            GROUP BY 1
        ) u ON u.year = yd.year

        LEFT JOIN (
            SELECT DATE_TRUNC('year', c_created_at) AS year, COUNT(*) AS ArtistCount
            FROM t_artist_profile
            GROUP BY 1
        ) a ON a.year = yd.year

        WHERE @type = 'YEARLY'

        ORDER BY Period;
        ";

            using var cmd = new NpgsqlCommand(query, _conn);
            cmd.Parameters.AddWithValue("@type", type.ToUpper());

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new t_UsersCount
                {
                    Period = reader.GetFieldValue<DateOnly>(0),
                    Label = reader["Label"]?.ToString(),
                    UserCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    ArtistCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                });
            }

            return list;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
        finally
        {
            await _conn.CloseAsync();
        }
    }
    public async Task<t_TotalCount> GetTotalUsersCount()
    {

        try
        {
            await _conn.OpenAsync();

            var query = @"
                   SELECT 
                   (SELECT COUNT(*) FROM t_user) AS TotalUsers,
                   (SELECT COUNT(*) FROM t_artist_profile) AS TotalArtists;";

            using var cmd = new NpgsqlCommand(query, _conn);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new t_TotalCount
                {
                    UserTotalCount = reader.GetInt32(0),
                    ArtistTotalCount = reader.GetInt32(1)
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
        finally
        {
            await _conn.CloseAsync();
        }
    }
    public async Task<List<t_SellingCategory>> TopSellingCategory()
    {
        List<t_SellingCategory> list = new();

        try
        {
            await _conn.OpenAsync();

            var query = @"SELECT 
            c.c_category_name,
            COUNT(oi.c_item_id) AS total_sales
        FROM t_order o
        JOIN t_order_item oi ON o.c_order_id = oi.c_order_id
        JOIN t_artwork a ON oi.c_artwork_id = a.c_artwork_id
        JOIN t_category c ON a.c_category_id = c.c_category_id
        GROUP BY c.c_category_name
        ORDER BY total_sales DESC;";

            using var cmd = new NpgsqlCommand(query, _conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync()) // 🔥 LOOP
            {
                list.Add(new t_SellingCategory
                {
                    Category = reader["c_category_name"].ToString(),
                    TotalSales = Convert.ToDecimal(reader["total_sales"])
                });
            }

            return list;
        }
        finally
        {
            await _conn.CloseAsync();
        }
    }
    public async Task<List<t_TopArtist>> TopPerformingArtist()
    {
        List<t_TopArtist> list = new List<t_TopArtist>();
        try
        {
            await _conn.OpenAsync();

            var query = @"SELECT 
                      ar.c_artist_name,
                      SUM(p.c_amount_paid) AS total_revenue,
                      COUNT(oi.c_item_id) AS total_sales
                  
                  FROM t_payment p
                  
                  JOIN t_order o 
                      ON p.c_order_id = o.c_order_id
                  
                  JOIN t_order_item oi 
                      ON o.c_order_id = oi.c_order_id
                  
                  JOIN t_artwork a 
                      ON oi.c_artwork_id = a.c_artwork_id
                  
                  JOIN t_artist_profile ar 
                      ON a.c_artist_id = ar.c_artist_id
                  
                  WHERE p.c_payment_status = 'SUCCESS'
                  
                  GROUP BY ar.c_artist_name
                  
                  ORDER BY total_revenue DESC
                  
                  LIMIT 5;";

            using var cmd = new NpgsqlCommand(query, _conn);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new t_TopArtist
                {
                    Name = reader["c_artist_name"].ToString(),
                    TotalRevenue = Convert.ToDecimal(reader["total_revenue"]),
                    TotalSales = Convert.ToDecimal(reader["total_sales"])
                });
            }

            return list;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
        finally
        {
            await _conn.CloseAsync();
        }
    }
    public async Task<List<t_RecentActivity>> RecentActivity()
    {
        List<t_RecentActivity> list = new List<t_RecentActivity>();
        try
        {
            await _conn.OpenAsync();

            var query = @"SELECT 
                              c_activity_type,
                              c_description,
                              c_created_at
                          FROM t_activity
                          ORDER BY c_created_at DESC
                          LIMIT 10;";

            using var cmd = new NpgsqlCommand(query, _conn);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new t_RecentActivity
                {
                    Activity_Type = reader["c_activity_type"].ToString(),
                    Description = reader["c_description"].ToString(),
                    Created_At = (DateOnly)reader["c_created_at"]
                });
            }
            return list;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
        finally
        {
            await _conn.CloseAsync();
        }
    }

}
