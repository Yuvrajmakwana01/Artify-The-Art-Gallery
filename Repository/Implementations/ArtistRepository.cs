using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
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

    
    }
}