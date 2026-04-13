using System;
using System.Collections.Generic;
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


        public async Task<int> Register(t_Artist data)
        {
            try
            {
                await _conn.OpenAsync();
                // Check for existing email
                NpgsqlCommand chk = new NpgsqlCommand("SELECT 1 FROM t_artist_profile WHERE c_artist_email = @email", _conn);
                chk.Parameters.AddWithValue("@email", data.c_Email);
                if (await chk.ExecuteScalarAsync() != null) return 0;

                string passwordHash = BCrypt.Net.BCrypt.HashPassword(data.c_Password);

                string sql = @"INSERT INTO t_artist_profile (c_artist_email, c_password, c_artist_name, c_username, c_gender, c_mobile, c_profile_image, c_cover_image, c_url, c_biography) 
               VALUES (@email, @pwd, @fname, @uname, @gender, @mobile, @img, @cover, @social, @biography)";

                using (NpgsqlCommand cmd = new NpgsqlCommand(sql, _conn))
                {
                    cmd.Parameters.AddWithValue("@email", data.c_Email);
                    cmd.Parameters.AddWithValue("@pwd", passwordHash);
                    cmd.Parameters.AddWithValue("@fname", data.c_Full_Name);
                    cmd.Parameters.AddWithValue("@uname", data.c_UserName);
                    cmd.Parameters.AddWithValue("@gender", data.c_Gender);
                    cmd.Parameters.AddWithValue("@mobile", data.c_Mobile ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@img", data.c_Profile_Image ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@cover", data.c_Cover_Image ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@social", new string[] { data.c_Social_Media_Link ?? "" });
                    cmd.Parameters.AddWithValue("@biography", data.c_BioGraphy ?? (object)DBNull.Value);

                    await cmd.ExecuteNonQueryAsync();
                    return 1;
                }
            }
            finally { await _conn.CloseAsync(); }
        }

        public async Task<t_Artist> Login(vm_Login user)
        {
        Console.WriteLine("Data" + user.c_Email + user.c_Password);
            t_Artist UserData = null; // Return null if login fails
            var qry = "SELECT * FROM t_artist_profile WHERE c_artist_email = @email";

            try
            {
                await _conn.OpenAsync();
                using (NpgsqlCommand com = new NpgsqlCommand(qry, _conn))
                {
                    com.Parameters.AddWithValue("@email", user.c_Email);
                    using (var reader = await com.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string storedHash = reader["c_password"].ToString();
                            if (BCrypt.Net.BCrypt.Verify(user.c_Password, storedHash))
                            {
                                UserData = new t_Artist
                                {
                                    // Ensure these column names match your actual DB schema
                                    c_User_Id = Convert.ToInt32(reader["c_artist_id"]),
                                    c_UserName = reader["c_username"].ToString(),
                                    c_Email = reader["c_artist_email"].ToString(),
                                    c_Full_Name = reader["c_artist_name"].ToString(),
                                    c_Profile_Image = reader["c_profile_image"]?.ToString()
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Login Error: " + ex.Message);
            }
            finally { await _conn.CloseAsync(); }
            return UserData;
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
                COALESCE(SUM(aw.c_sell_count), 0) AS total_sells

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
                                c_TotalSellCount = reader.GetInt32(8)
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
        public async Task<int> EditArtistProfile(t_ArtistProfile data)
        {
            try
            {
                await _conn.OpenAsync();

                Console.WriteLine("Editing ArtistId: " + data.ArtistId);

                var cmd = new NpgsqlCommand(@"
                    UPDATE t_artist_profile SET
                        c_artist_name = @name,
                        c_biography   = @bio,
                        c_cover_image = COALESCE(@cover, c_cover_image),
                        c_url         = @urls,
                        c_is_active   = @active
                    WHERE c_artist_id = @id", _conn);

                cmd.Parameters.AddWithValue("@id", data.ArtistId);

                cmd.Parameters.AddWithValue("@name",
                    string.IsNullOrEmpty(data.ArtistName)
                        ? (object)DBNull.Value
                        : data.ArtistName);

                cmd.Parameters.AddWithValue("@bio",
                    string.IsNullOrEmpty(data.Biography)
                        ? (object)DBNull.Value
                        : data.Biography);

                cmd.Parameters.AddWithValue("@cover",
                    string.IsNullOrEmpty(data.CoverImage)
                        ? (object)DBNull.Value
                        : data.CoverImage);

                // ✅ FIXED ARRAY HANDLING
                if (data.Urls != null && data.Urls.Length > 0)
                {
                    cmd.Parameters.AddWithValue("@urls",
                        NpgsqlDbType.Array | NpgsqlDbType.Text,
                        data.Urls);
                }
                else
                {
                    cmd.Parameters.AddWithValue("@urls", DBNull.Value);
                }

                cmd.Parameters.AddWithValue("@active", data.IsActive);

                int rows = await cmd.ExecuteNonQueryAsync();

                Console.WriteLine("Rows affected: " + rows);

                return rows > 0 ? 1 : 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("EditArtistProfile ERROR: " + ex.Message);
                return -1;
            }
        }

        // ✅ GET PROFILE
        public async Task<t_ArtistProfile> GetArtistById(int artistId)
        {
            var profile = new t_ArtistProfile();

            try
            {
                await _conn.OpenAsync();

                var cmd = new NpgsqlCommand(@"
                    SELECT * FROM t_artist_profile 
                    WHERE c_artist_id = @id", _conn);

                cmd.Parameters.AddWithValue("@id", artistId);

                await using var reader = await cmd.ExecuteReaderAsync();

                if (reader.Read())
                {
                    profile.ArtistId = (int)reader["c_artist_id"];
                    profile.ArtistName = reader["c_artist_name"]?.ToString();
                    profile.ArtistEmail = reader["c_artist_email"]?.ToString();

                    profile.Biography = reader["c_biography"] as string;
                    profile.CoverImage = reader["c_cover_image"] as string;

                    profile.RatingAvg = reader["c_rating_avg"] == DBNull.Value
                        ? 0
                        : (decimal)reader["c_rating_avg"];

                    profile.IsVerified = reader["c_is_verified"] != DBNull.Value &&
                                         (bool)reader["c_is_verified"];

                    profile.IsActive = reader["c_is_active"] != DBNull.Value &&
                                       (bool)reader["c_is_active"];

                    profile.Urls = reader["c_url"] as string[];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetArtistById ERROR: " + ex.Message);
            }

            return profile;
        }
    }
}