using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Org.BouncyCastle.Crypto.Generators;
using Repository.Interfaces;
using Repository.Models;

namespace Repository.Implementations
{
    public class ArtistRepository : IArtistInterface
    {

        private readonly NpgsqlConnection _conn;

        public ArtistRepository(NpgsqlConnection connection)
        {
            _conn = connection;
        }


        // public async Task<int> Register(t_Artist data)
        // {
        //     try
        //     {
        //         await _conn.OpenAsync();
        //         // Check for existing email
        //         NpgsqlCommand chk = new NpgsqlCommand("SELECT 1 FROM t_artist_profile WHERE c_artist_email = @email", _conn);
        //         chk.Parameters.AddWithValue("@email", data.c_Email);
        //         if (await chk.ExecuteScalarAsync() != null) return 0;

        //         string passwordHash = BCrypt.Net.BCrypt.HashPassword(data.c_Password);

        //         string sql = @"INSERT INTO t_artist_profile (c_artist_email, c_password, c_artist_name, c_username, c_gender, c_mobile, c_profile_image, c_cover_image, c_url, c_biography) 
        //        VALUES (@email, @pwd, @fname, @uname, @gender, @mobile, @img, @cover, @social, @biography)";

        //         using (NpgsqlCommand cmd = new NpgsqlCommand(sql, _conn))
        //         {
        //             cmd.Parameters.AddWithValue("@email", data.c_Email);
        //             cmd.Parameters.AddWithValue("@pwd", passwordHash);
        //             cmd.Parameters.AddWithValue("@fname", data.c_Full_Name);
        //             cmd.Parameters.AddWithValue("@uname", data.c_UserName);
        //             cmd.Parameters.AddWithValue("@gender", data.c_Gender);
        //             cmd.Parameters.AddWithValue("@mobile", data.c_Mobile ?? (object)DBNull.Value);
        //             cmd.Parameters.AddWithValue("@img", data.c_Profile_Image ?? (object)DBNull.Value);
        //             cmd.Parameters.AddWithValue("@cover", data.c_Cover_Image ?? (object)DBNull.Value);
        //             cmd.Parameters.AddWithValue("@social", new string[] { data.c_Social_Media_Link ?? "" });
        //             cmd.Parameters.AddWithValue("@biography", data.c_BioGraphy ?? (object)DBNull.Value);

        //             await cmd.ExecuteNonQueryAsync();
        //             return 1;
        //         }
        //     }
        //     finally { await _conn.CloseAsync(); }
        // }

        // public async Task<t_Artist> Login(vm_Login user)
        // {
        //     Console.WriteLine("Data" + user.c_Email + user.c_Password);
        //     t_Artist UserData = null; // Return null if login fails
        //     var qry = "SELECT * FROM t_artist_profile WHERE c_artist_email = @email";

        //     try
        //     {
        //         await _conn.OpenAsync();
        //         using (NpgsqlCommand com = new NpgsqlCommand(qry, _conn))
        //         {
        //             com.Parameters.AddWithValue("@email", user.c_Email);
        //             using (var reader = await com.ExecuteReaderAsync())
        //             {
        //                 if (await reader.ReadAsync())
        //                 {
        //                     string storedHash = reader["c_password"].ToString();
        //                     if (BCrypt.Net.BCrypt.Verify(user.c_Password, storedHash))
        //                     {
        //                         UserData = new t_Artist
        //                         {
        //                             // Ensure these column names match your actual DB schema
        //                             c_User_Id = Convert.ToInt32(reader["c_artist_id"]),
        //                             c_UserName = reader["c_username"].ToString(),
        //                             c_Email = reader["c_artist_email"].ToString(),
        //                             c_Full_Name = reader["c_artist_name"].ToString(),
        //                             c_Profile_Image = reader["c_profile_image"]?.ToString()
        //                         };
        //                     }
        //                 }
        //             }
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine("Login Error: " + ex.Message);
        //     }
        //     finally { await _conn.CloseAsync(); }
        //     return UserData;
        // }


        public async Task<int> Register(t_Artist data)
        {
            try
            {
                await _conn.OpenAsync();
                // Check redundancy
                var chk = new NpgsqlCommand("SELECT 1 FROM t_artist_profile WHERE c_artist_email = @email", _conn);
                chk.Parameters.AddWithValue("@email", data.c_Email);
                if (await chk.ExecuteScalarAsync() != null) return 0;

                string sql = @"INSERT INTO t_artist_profile 
                    (c_artist_email, c_password, c_artist_name, c_username, c_gender, c_mobile, c_profile_image, c_cover_image, c_url, c_biography, c_is_active) 
                    VALUES (@email, @pwd, @fname, @uname, @gender, @mobile, @img, @cover, @social, @bio, FALSE)";

                using var cmd = new NpgsqlCommand(sql, _conn);
                cmd.Parameters.AddWithValue("@email", data.c_Email);
                cmd.Parameters.AddWithValue("@pwd", BCrypt.Net.BCrypt.HashPassword(data.c_Password));
                cmd.Parameters.AddWithValue("@fname", data.c_Full_Name);
                cmd.Parameters.AddWithValue("@uname", data.c_UserName);
                cmd.Parameters.AddWithValue("@gender", data.c_Gender);
                cmd.Parameters.AddWithValue("@mobile", (object)data.c_Mobile ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@img", (object)data.c_Profile_Image ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cover", (object)data.c_Cover_Image ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@social", new string[] { data.c_Social_Media_Link ?? "" });
                cmd.Parameters.AddWithValue("@bio", (object)data.c_BioGraphy ?? DBNull.Value);

                return await cmd.ExecuteNonQueryAsync();
            }
            finally { await _conn.CloseAsync(); }
        }


        public async Task<t_Artist> Login(vm_Login user)
        {
            t_Artist? artist = null;

            // Select all necessary columns to populate the t_Artist object
            const string sql = @"
                SELECT 
                    c_artist_id, 
                    c_username, 
                    c_artist_email, 
                    c_password, 
                    c_artist_name, 
                    c_profile_image, 
                    c_is_active 
                FROM t_artist_profile 
                WHERE c_artist_email = @email";

            try
            {
                if (_conn.State != ConnectionState.Open) await _conn.OpenAsync();

                using var cmd = new NpgsqlCommand(sql, _conn);
                cmd.Parameters.AddWithValue("@email", user.c_Email);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    // 1. Extract the hashed password from the DB
                    string storedHash = reader["c_password"].ToString() ?? string.Empty;

                    // 2. Verify the plain text password against the hash
                    if (BCrypt.Net.BCrypt.Verify(user.c_Password, storedHash))
                    {
                        // 3. Populate the model if verification succeeds
                        artist = new t_Artist
                        {
                            c_User_Id = Convert.ToInt32(reader["c_artist_id"]),
                            c_UserName = reader["c_username"].ToString() ?? "",
                            c_Email = reader["c_artist_email"].ToString() ?? "",
                            c_Full_Name = reader["c_artist_name"].ToString() ?? "",
                            c_Profile_Image = reader["c_profile_image"]?.ToString(),
                            
                            // Map the boolean status for the "Awaiting Approval" check
                            c_Is_Active = !reader.IsDBNull(reader.GetOrdinal("c_is_active")) && reader.GetBoolean(reader.GetOrdinal("c_is_active"))
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception as per your logging strategy
                Console.WriteLine($"Database Login Error: {ex.Message}");
                throw; 
            }
            finally
            {
                if (_conn.State == ConnectionState.Open) await _conn.CloseAsync();
            }

            // Returns null if user not found OR password incorrect
            return artist;
        }

        
        public async Task<t_Artist_Dashboard> GetDashboardData(int artistId)
        {
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                var qry = @"
            SELECT 
                ap.c_artist_id,
                ap.c_artist_name,
                ap.c_artist_email,
                ap.c_biography,
                ap.c_cover_image,
                ap.c_rating_avg,

                COUNT(aw.c_artwork_id) AS total_artworks,
                COALESCE(SUM(aw.c_likes_count), 0) AS total_likes,
                COALESCE(SUM(aw.c_sell_count), 0) AS total_sells,
                COALESCE(SUM(aw.c_sell_count * aw.c_price), 0) AS total_earnings

            FROM t_artist_profile ap
            LEFT JOIN t_artwork aw 
                ON ap.c_artist_id = aw.c_artist_id

            WHERE ap.c_artist_id = @artistId
            GROUP BY 
                ap.c_artist_id,
                ap.c_artist_name,
                ap.c_artist_email,
                ap.c_biography,
                ap.c_cover_image,
                ap.c_rating_avg;
        ";

                using (var cmd = new NpgsqlCommand(qry, _conn))
                {
                    cmd.Parameters.AddWithValue("@artistId", artistId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new t_Artist_Dashboard
                            {
                                c_ArtisrtId = reader.GetInt32(0),
                                c_ArtistName = reader.GetString(1),
                                c_Email = reader.GetString(2),
                                c_Biography = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                c_CoverImageName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                c_RatingAvg = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),

                                c_TotalArtworkCount = reader.GetInt32(6),
                                c_TotalLikeCount = reader.GetInt32(7),
                                c_TotalSellCount = reader.GetInt32(8),
                                c_TotalEarning = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9)
                            };
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching artist dashboard data", ex);
            }
        }

        // ✅ SINGLE CLEAN METHOD
        // ✅ FIXED: Added proper Connection Management (Open/Close) and Array Mapping
        public async Task<int> EditArtistProfile(t_ArtistProfile data)
        {
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                // Use a string builder or a clean string to avoid hidden character issues
                string sql = @"
            UPDATE t_artist_profile SET
                c_artist_name   = @name,
                c_biography     = @bio,
                c_cover_image   = COALESCE(@cover, c_cover_image),
                c_profile_image = COALESCE(@pic,   c_profile_image),
                c_url           = @urls
            WHERE c_artist_id = @id";


                using (var cmd = new NpgsqlCommand(sql, _conn))
                {
                    cmd.Parameters.AddWithValue("@id",    data.ArtistId);
                    cmd.Parameters.AddWithValue("@name",  (object)data.ArtistName  ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@bio",   (object)data.Biography   ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@cover", (object)data.CoverImage  ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pic",   (object)data.ProfilePicture ?? DBNull.Value);

                    // FORCE the NpgsqlDbType to ensure the driver knows it is a Text Array
                    var urlParam = new NpgsqlParameter("@urls", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text);
                    urlParam.Value = (object)data.Urls ?? DBNull.Value;
                    cmd.Parameters.Add(urlParam);

                    int rows = await cmd.ExecuteNonQueryAsync();
                    return rows > 0 ? 1 : 0;
                }

            }
            catch (Exception ex)
            {
                // This will now print the SPECIFIC database error (e.g., "column c_url does not exist")
                Console.WriteLine("EditArtistProfile DATABASE ERROR: " + ex.Message);
                return -1;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        public async Task<t_ArtistProfile> GetArtistById(int artistId)
        {
            var profile = new t_ArtistProfile();
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                // c_profile_image lives directly in t_artist_profile — no JOIN needed
                var qry = @"SELECT c_artist_id, c_artist_name, c_artist_email, c_biography,
                           c_cover_image, c_rating_avg, c_is_verified, c_url,
                           c_profile_image
                    FROM t_artist_profile
                    WHERE c_artist_id = @id";


                using (var cmd = new NpgsqlCommand(qry, _conn))
                {
                    cmd.Parameters.AddWithValue("@id", artistId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            profile.ArtistId = reader.GetInt32(0);
                            profile.ArtistName = reader.IsDBNull(1) ? null : reader.GetString(1);
                            profile.ArtistEmail = reader.IsDBNull(2) ? null : reader.GetString(2);
                            profile.Biography = reader.IsDBNull(3) ? null : reader.GetString(3);
                            profile.CoverImage = reader.IsDBNull(4) ? null : reader.GetString(4);
                            profile.RatingAvg = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5);
                            profile.IsVerified = !reader.IsDBNull(6) && reader.GetBoolean(6);
                            profile.Urls = reader.IsDBNull(7) ? null : (string[])reader.GetValue(7);
                            profile.ProfilePicture = reader.IsDBNull(8) ? null : reader.GetString(8);
                        }
                    }
                }
            }
            finally { await _conn.CloseAsync(); }
            return profile;
        }



        // Yuvi




        public async Task<List<object>> GetMonthlyRevenue(int artistId)
        {
            var result = new List<object>();

            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                var qry = @"
        SELECT 
            DATE_PART('month', aw.c_created_at) AS month_number,
            TO_CHAR(aw.c_created_at, 'Mon') AS month_name,
            COALESCE(SUM(aw.c_sell_count * aw.c_price), 0) AS revenue
        FROM t_artwork aw
        WHERE aw.c_artist_id = @artistId
        AND DATE_PART('year', aw.c_created_at) = DATE_PART('year', CURRENT_DATE)
        GROUP BY month_number, month_name
        ORDER BY month_number;
        ";

                using (var cmd = new NpgsqlCommand(qry, _conn))
                {
                    cmd.Parameters.AddWithValue("@artistId", artistId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(new
                            {
                                month = reader.GetString(1),   // Jan, Feb...
                                revenue = reader.GetDecimal(2)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return result;
        }

        public async Task<List<object>> GetSalesByCategory(int artistId)
        {
            var result = new List<object>();

            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                var qry = @"
        SELECT 
            c.c_category_name,
            COALESCE(SUM(aw.c_sell_count), 0) AS total_sales
        FROM t_artwork aw
        JOIN t_category c ON aw.c_category_id = c.c_category_id
        WHERE aw.c_artist_id = @artistId
        GROUP BY c.c_category_name;
        ";

                using (var cmd = new NpgsqlCommand(qry, _conn))
                {
                    cmd.Parameters.AddWithValue("@artistId", artistId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(new
                            {
                                category = reader.GetString(0),
                                sales = reader.GetInt32(1)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return result;
        }


        public async Task<int> ChangePassword(int artistId, string oldPwd, string newPwd)
        {
            try
            {
                await _conn.OpenAsync();
                // 1. Fetch current hashed password from DB
                var cmd = new NpgsqlCommand("SELECT c_password FROM t_artist_profile WHERE c_artist_id = @id", _conn);
                cmd.Parameters.AddWithValue("@id", artistId);
                string currentHashed = (string)await cmd.ExecuteScalarAsync();

                // 2. Verify Old Password
                if (!BCrypt.Net.BCrypt.Verify(oldPwd, currentHashed)) return 0;

                // 3. Update with New Hash
                string newHashed = BCrypt.Net.BCrypt.HashPassword(newPwd);
                var upCmd = new NpgsqlCommand("UPDATE t_artist_profile SET c_password = @pwd WHERE c_artist_id = @id", _conn);
                upCmd.Parameters.AddWithValue("@pwd", newHashed);
                upCmd.Parameters.AddWithValue("@id", artistId);

                return await upCmd.ExecuteNonQueryAsync();
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }



        public async Task<t_Artist_EarningsSummary> GetEarningsSummary(int artistId)
        {
            var result = new t_Artist_EarningsSummary();

            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                var qry = @"
        SELECT 
            -- AVAILABLE
            COALESCE(SUM(CASE 
                WHEN p.c_status = 'pending' AND p.c_paid = 'pending' 
                THEN p.c_net_amount ELSE 0 END), 0) AS available_balance,

            -- REQUESTED
            COALESCE(SUM(CASE 
                WHEN p.c_status = 'pending' AND p.c_paid = 'requested' 
                THEN p.c_net_amount ELSE 0 END), 0) AS requested_balance,

            -- SUCCESS / REVENUE
            COALESCE(SUM(CASE 
                WHEN p.c_status = 'success' AND p.c_paid = 'success' 
                THEN p.c_net_amount ELSE 0 END), 0) AS revenue_balance

        FROM t_payout p
        INNER JOIN t_order_item oi 
            ON p.c_order_id = oi.c_order_id

        WHERE p.c_artist_id = @artistId;
        ";

                using (var cmd = new NpgsqlCommand(qry, _conn))
                {
                    cmd.Parameters.AddWithValue("@artistId", artistId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            result.AvailableBalance = reader.GetDecimal(0);
                            result.RequestedBalance = reader.GetDecimal(1);
                            result.RevenueBalance = reader.GetDecimal(2);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return result;
        }
        /// <summary>
        /// Soft-deactivates an artist account by setting c_is_active = false.
        /// The artist can be reactivated by an admin later.
        /// </summary>
        public async Task<int> DeactivateAccount(int artistId)
        {
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                const string sql = @"UPDATE t_artist_profile
                                     SET    c_is_active = FALSE
                                     WHERE  c_artist_id = @id";

                using var cmd = new NpgsqlCommand(sql, _conn);
                cmd.Parameters.AddWithValue("@id", artistId);
                var rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0 ? 1 : 0;   // 1 = success, 0 = artist not found
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeactivateAccount ERROR: {ex.Message}");
                return -1;
            }
            finally
            {
                if (_conn.State == System.Data.ConnectionState.Open)
                    await _conn.CloseAsync();
            }
        }
    }
}