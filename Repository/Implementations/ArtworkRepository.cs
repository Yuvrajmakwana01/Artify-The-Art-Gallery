using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Repository.Interfaces;
using Repository.Models;

namespace Repository.Implementations
{
    public class ArtworkRepository : IArtworkInterface
    {
        private readonly NpgsqlConnection _conn;

        public ArtworkRepository(NpgsqlConnection connection)
        {
            _conn = connection;
        }

        public async Task<int> UploadArtwork(t_Artwork art)
        {
            string sql = @"INSERT INTO t_artwork 
                   (c_artist_id, c_category_id, c_title, c_description, c_price, c_preview_path, c_original_path, c_approval_status) 
                   VALUES (@aid, @cid, @title, @desc, @price, @prev, @orig, @status)";

            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                using (var cmd = new NpgsqlCommand(sql, _conn))
                {
                    cmd.Parameters.AddWithValue("@aid", art.c_artist_id);
                    cmd.Parameters.AddWithValue("@cid", art.c_category_id);
                    cmd.Parameters.AddWithValue("@title", art.c_title);
                    cmd.Parameters.AddWithValue("@desc", art.c_description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@price", art.c_price);
                    cmd.Parameters.AddWithValue("@prev", art.c_preview_path ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@orig", art.c_original_path ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@status", "Pending");

                    return await cmd.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        public async Task<IEnumerable<dynamic>> GetCategories()
        {
            var categories = new List<dynamic>();
            string sql = "SELECT c_category_id, c_category_name FROM t_category WHERE c_is_active = 'Active'";

            using (var cmd = new NpgsqlCommand(sql, _conn))
            {
                await _conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        categories.Add(new
                        {
                            id = reader["c_category_id"],
                            name = reader["c_category_name"]
                        });
                    }
                }
                await _conn.CloseAsync();
            }
            return categories;
        }

        public async Task<IEnumerable<vm_Artwork>> GetAllArtworks()
        {
            List<vm_Artwork> artworkList = new List<vm_Artwork>();

            string sql = @"SELECT a.*, c.c_category_name as CategoryName, u.c_full_name as ArtistName 
                   FROM t_artwork a
                   LEFT JOIN t_category c ON a.c_category_id = c.c_category_id
                   LEFT JOIN t_user u ON a.c_artist_id = u.c_user_id
                   ORDER BY a.c_created_at DESC";

            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                using (var cmd = new NpgsqlCommand(sql, _conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            artworkList.Add(new vm_Artwork
                            {
                                c_artwork_id = reader.IsDBNull(reader.GetOrdinal("c_artwork_id")) ? 0 : (int)reader["c_artwork_id"],
                                c_title = reader["c_title"]?.ToString() ?? "Untitled",
                                c_description = reader["c_description"]?.ToString() ?? "",
                                c_price = reader["c_price"] != DBNull.Value ? Convert.ToDecimal(reader["c_price"]) : 0m,
                                c_preview_path = reader["c_preview_path"]?.ToString() ?? "",
                                c_likes_count = reader["c_likes_count"] != DBNull.Value ? Convert.ToInt32(reader["c_likes_count"]) : 0,
                                CategoryName = reader["CategoryName"]?.ToString() ?? "Uncategorized",
                                ArtistName = reader["ArtistName"]?.ToString() ?? "Unknown Artist"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            finally { await _conn.CloseAsync(); }

            return artworkList;
        }

        public async Task<IEnumerable<t_Artwork>> GetApprovedArtworks()
        {
            var list = new List<t_Artwork>();
            string sql = "SELECT * FROM t_artwork WHERE c_approval_status = 'Approved'";

            await _conn.OpenAsync();
            using (var cmd = new NpgsqlCommand(sql, _conn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(new t_Artwork
                    {
                        c_artwork_id = (int)reader["c_artwork_id"],
                        c_title = reader["c_title"].ToString()
                    });
                }
            }
            await _conn.CloseAsync();
            return list;
        }

        // ✅ FIXED: Added all missing columns including c_preview_path
        public async Task<IEnumerable<t_Artwork>> GetArtworksByArtist(int artistId)
        {
            var list = new List<t_Artwork>();
            string sql = @"SELECT c_artwork_id, c_artist_id, c_category_id, c_title, c_description, 
                                  c_price, c_preview_path, c_original_path,
                                  COALESCE(NULLIF(BTRIM(c_approval_status), ''), 'Pending') AS c_approval_status, 
                                  c_admin_note, c_likes_count, c_sell_count
                           FROM t_artwork 
                           WHERE c_artist_id = @aid 
                           ORDER BY c_artwork_id DESC";

            await _conn.OpenAsync();
            using (var cmd = new NpgsqlCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("aid", artistId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var artwork = new t_Artwork
                        {
                            c_artwork_id = reader.GetInt32(reader.GetOrdinal("c_artwork_id")),
                            c_artist_id = reader.GetInt32(reader.GetOrdinal("c_artist_id")),
                            c_category_id = reader.GetInt32(reader.GetOrdinal("c_category_id")),
                            c_title = reader.GetString(reader.GetOrdinal("c_title")),
                            c_description = reader.IsDBNull(reader.GetOrdinal("c_description")) ? "" : reader.GetString(reader.GetOrdinal("c_description")),
                            c_price = reader.GetDecimal(reader.GetOrdinal("c_price")),
                            c_approval_status = reader.GetString(reader.GetOrdinal("c_approval_status")),
                            c_preview_path = reader.IsDBNull(reader.GetOrdinal("c_preview_path")) ? null : reader.GetString(reader.GetOrdinal("c_preview_path")),
                            c_original_path = reader.IsDBNull(reader.GetOrdinal("c_original_path")) ? null : reader.GetString(reader.GetOrdinal("c_original_path")),
                            c_admin_note = reader.IsDBNull(reader.GetOrdinal("c_admin_note")) ? null : reader.GetString(reader.GetOrdinal("c_admin_note")),
                            c_likes_count = reader.IsDBNull(reader.GetOrdinal("c_likes_count")) ? 0 : reader.GetInt32(reader.GetOrdinal("c_likes_count")),
                            c_sell_count = reader.IsDBNull(reader.GetOrdinal("c_sell_count")) ? 0 : reader.GetInt32(reader.GetOrdinal("c_sell_count"))
                        };
                        list.Add(artwork);
                    }
                }
            }
            await _conn.CloseAsync();
            return list;
        }

        public async Task<int> DeleteArtwork(int artworkId)
        {
            string sql = "DELETE FROM t_artwork WHERE c_artwork_id = @id";
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                using (var cmd = new NpgsqlCommand(sql, _conn))
                {
                    cmd.Parameters.AddWithValue("id", artworkId);
                    return await cmd.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        public async Task<int> UpdateArtwork(t_Artwork art)
        {
            string sql = @"UPDATE t_artwork 
                   SET c_title = @title, 
                       c_description = @desc, 
                       c_price = @price,
                       c_approval_status = 'Pending',
                       c_category_id = @cid,
                       c_preview_path = COALESCE(@prev, c_preview_path),
                       c_original_path = COALESCE(@orig, c_original_path)
                   WHERE c_artwork_id = @id";

            await _conn.OpenAsync();
            using (var cmd = new NpgsqlCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("title", art.c_title);
                cmd.Parameters.AddWithValue("desc", art.c_description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("price", art.c_price);
                cmd.Parameters.AddWithValue("id", art.c_artwork_id);
                cmd.Parameters.AddWithValue("cid", art.c_category_id);
                cmd.Parameters.AddWithValue("prev", art.c_preview_path ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("orig", art.c_original_path ?? (object)DBNull.Value);

                return await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<t_Artwork> GetById(int id)
        {
            string sql = @"SELECT c_artwork_id, c_artist_id, c_category_id, c_title, c_description, 
                                  c_price, c_preview_path, c_original_path,
                                  COALESCE(NULLIF(BTRIM(c_approval_status), ''), 'Pending') AS c_approval_status, 
                                  c_admin_note, c_likes_count, c_sell_count
                           FROM t_artwork 
                           WHERE c_artwork_id = @id";

            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                using (var cmd = new NpgsqlCommand(sql, _conn))
                {
                    cmd.Parameters.AddWithValue("id", id);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new t_Artwork
                            {
                                c_artwork_id = (int)reader["c_artwork_id"],
                                c_artist_id = (int)reader["c_artist_id"],
                                c_category_id = (int)reader["c_category_id"],
                                c_title = reader["c_title"]?.ToString(),
                                c_description = reader["c_description"]?.ToString(),
                                c_price = Convert.ToDecimal(reader["c_price"]),
                                c_preview_path = reader["c_preview_path"]?.ToString(),
                                c_original_path = reader["c_original_path"]?.ToString(),
                                c_approval_status = reader["c_approval_status"]?.ToString(),
                                c_admin_note = reader["c_admin_note"]?.ToString(),
                                c_likes_count = reader["c_likes_count"] != DBNull.Value ? Convert.ToInt32(reader["c_likes_count"]) : 0,
                                c_sell_count = reader["c_sell_count"] != DBNull.Value ? Convert.ToInt32(reader["c_sell_count"]) : 0
                            };
                        }
                    }
                }
            }
            finally
            {
                await _conn.CloseAsync();
            }
            return null;
        }
    }
}
