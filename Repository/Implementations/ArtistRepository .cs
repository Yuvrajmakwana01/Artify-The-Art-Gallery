using Npgsql;
using NpgsqlTypes;
using Repository.Interfaces;
using Repository.Models;
using System;
using System.Threading.Tasks;

namespace Repository.Implementations
{
    public class ArtistRepository : IArtistInterface
    {
        private readonly NpgsqlConnection _conn;

        public ArtistRepository (NpgsqlConnection connectionString)
        {
            _conn = connectionString;
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