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
                   (c_artist_id, c_category_id, c_title, c_description, c_price, c_preview_path, c_original_path) 
                   VALUES (@aid, @cid, @title, @desc, @price, @prev, @orig)";

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

            // Removed the WHERE clause or changed it to include Pending
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
                                // Use reader.GetFieldValue<int> or explicit casting with null checks
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
                // Log the error so you can see if a column name is misspelled
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

        public async Task<IEnumerable<t_Artwork>> GetArtworksByArtist(int artistId)
        {
            var list = new List<t_Artwork>();
            string sql = "SELECT * FROM t_artwork WHERE c_artist_id = @aid";

            await _conn.OpenAsync();
            using (var cmd = new NpgsqlCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("aid", artistId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) list.Add(new t_Artwork
                    {
                        c_artwork_id = reader.GetInt32(reader.GetOrdinal("c_artwork_id")),
                        c_artist_id = reader.GetInt32(reader.GetOrdinal("c_artist_id")),
                        c_category_id = reader.GetInt32(reader.GetOrdinal("c_category_id")),
                        c_title = reader.GetString(reader.GetOrdinal("c_title")),
                        c_description = reader.IsDBNull(reader.GetOrdinal("c_description")) ? "" : reader.GetString(reader.GetOrdinal("c_description")),
                        c_price = reader.GetDecimal(reader.GetOrdinal("c_price")),
                        c_approval_status = reader.GetString(reader.GetOrdinal("c_approval_status"))


                    });
                }
            }
            await _conn.CloseAsync();
            return list;
        }

        public async Task<int> DeleteArtwork(int artworkId)
        {
            string sql = "DELETE FROM t_artwork WHERE c_artwork_id = @id";
            await _conn.OpenAsync();
            using (var cmd = new NpgsqlCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("id", artworkId);
                return await cmd.ExecuteNonQueryAsync();
            }
        }



        public async Task<int> UpdateArtwork(t_Artwork art)
        {
            // The SQL uses COALESCE: if @prev is null, use the current c_preview_path
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
                cmd.Parameters.AddWithValue("approval_status", art.c_approval_status ?? (object)DBNull.Value);

                // Pass null if no file was uploaded
                cmd.Parameters.AddWithValue("prev", art.c_preview_path ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("orig", art.c_original_path ?? (object)DBNull.Value);

                return await cmd.ExecuteNonQueryAsync();
            }
        }


        public async Task<t_Artwork> GetById(int id)
        {
            string sql = "SELECT * FROM t_artwork WHERE c_artwork_id = @id";

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
                                c_original_path = reader["c_original_path"]?.ToString()
                            };
                        }
                    }
                }
            }
            finally
            {
                await _conn.CloseAsync();
            }
            return null; // Return null if artwork not found
        }




        // public async Task<IEnumerable<vm_Artwork>> GetAllArtworks(int artistId) // Add parameter
        // {
        //     List<vm_Artwork> artworkList = new List<vm_Artwork>();

        //     // Add WHERE a.c_artist_id = @artistId
        //     string sql = @"SELECT a.*, c.c_category_name as CategoryName, u.c_full_name as ArtistName 
        //            FROM t_artwork a
        //            LEFT JOIN t_category c ON a.c_category_id = c.c_category_id
        //            LEFT JOIN t_user u ON a.c_artist_id = u.c_user_id
        //            WHERE a.c_artist_id = @artistId
        //            ORDER BY a.c_created_at DESC";

        //     try
        //     {
        //         if (_conn.State != System.Data.ConnectionState.Open)
        //             await _conn.OpenAsync();

        //         using (var cmd = new NpgsqlCommand(sql, _conn))
        //         {
        //             // Bind the parameter to prevent SQL Injection
        //             cmd.Parameters.AddWithValue("artistId", artistId);

        //             using (var reader = await cmd.ExecuteReaderAsync())
        //             {
        //                 while (await reader.ReadAsync())
        //                 {
        //                     artworkList.Add(new vm_Artwork
        //                     {
        //                         c_artwork_id = reader.IsDBNull(reader.GetOrdinal("c_artwork_id")) ? 0 : (int)reader["c_artwork_id"],
        //                         c_title = reader["c_title"]?.ToString() ?? "Untitled",
        //                         c_description = reader["c_description"]?.ToString() ?? "",
        //                         c_price = reader["c_price"] != DBNull.Value ? Convert.ToDecimal(reader["c_price"]) : 0m,
        //                         c_preview_path = reader["c_preview_path"]?.ToString() ?? "",
        //                         c_likes_count = reader["c_likes_count"] != DBNull.Value ? Convert.ToInt32(reader["c_likes_count"]) : 0,
        //                         CategoryName = reader["CategoryName"]?.ToString() ?? "Uncategorized",
        //                         ArtistName = reader["ArtistName"]?.ToString() ?? "Unknown Artist"
        //                     });
        //                 }
        //             }
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         System.Diagnostics.Debug.WriteLine(ex.Message);
        //     }
        //     finally { await _conn.CloseAsync(); }

        //     return artworkList;
        // }
    }
}