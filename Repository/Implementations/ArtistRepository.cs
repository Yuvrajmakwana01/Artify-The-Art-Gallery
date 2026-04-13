using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Repository.Interfaces;
using Repository.Models;

namespace Repository.Implementations
{
    public class ArtistRepository : IArtistInterface
    {

        private readonly NpgsqlConnection _conn;

        public ArtistRepository(NpgsqlConnection conn)
        {
            _conn = conn;
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
    }
}