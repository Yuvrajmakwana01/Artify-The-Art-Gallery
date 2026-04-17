 using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Repository.Interfaces;
using Repository.Models;

namespace Repository.Implementations
{
    public class UserProfileRepository : IUserProfileInterface
    {
        private readonly NpgsqlConnection _conn;

        public UserProfileRepository(NpgsqlConnection conn)
        {
            _conn = conn;
        }

        // public async Task<UserProfile?> GetProfileById(int userId)
        // {
        //     UserProfile? profile = null;

        //     var query = @"SELECT c_user_id, c_username, c_email, c_full_name, 
        //                             c_mobile, c_gender, c_profile_image, c_created_at
        //                     FROM t_user 
        //                     WHERE c_user_id = @c_user_id;";
        //     try
        //     {
        //         await _conn.CloseAsync();
        //         await _conn.OpenAsync();
        //         using (var cmd = new NpgsqlCommand(query, _conn))
        //         {
        //             cmd.Parameters.AddWithValue("@c_user_id", userId);
        //             using var reader = await cmd.ExecuteReaderAsync();
        //             if (await reader.ReadAsync())
        //             {
        //                 profile = new UserProfile
        //                 {
        //                     c_UserId    = (int)reader["c_user_id"],
        //                     c_UserName  = reader["c_username"]     as string,
        //                     c_Email     = reader["c_email"]        as string,
        //                     c_FullName  = reader["c_full_name"]    as string,
        //                     c_Mobile    = reader["c_mobile"]       as string,
        //                     c_Gender    = reader["c_gender"]       as string,
        //                     c_Image     = reader["c_profile_image"] as string,
        //                     c_CreatedAt = reader["c_created_at"]   as DateTime?
        //                 };
        //             }
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine("GetProfile Error: " + ex.Message);
        //     }
        //     finally
        //     {
        //         await _conn.CloseAsync();
        //     }

        //     return profile;
        // }

        public async Task<UserProfile?> GetProfileById(int userId)
        {
            UserProfile? profile = null;
            var query = @"SELECT c_user_id, c_username, c_email, c_full_name, 
                                 c_mobile, c_gender, c_profile_image, c_created_at
                          FROM t_user 
                          WHERE c_user_id = @c_user_id;";
            try
            {
                await _conn.CloseAsync();
                await _conn.OpenAsync();
                using (var cmd = new NpgsqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("@c_user_id", userId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        profile = new UserProfile
                        {
                            c_UserId   = (int)reader["c_user_id"],
                            c_UserName = reader["c_username"]      as string,
                            c_Email    = reader["c_email"]         as string,
                            c_FullName = reader["c_full_name"]     as string,
                            c_Mobile   = reader["c_mobile"]        as string,
                            c_Gender   = reader["c_gender"]        as string,
                            c_Image    = reader["c_profile_image"] as string,
                            c_CreatedAt = reader["c_created_at"]   as DateTime?
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetProfile Error: " + ex.Message);
            }
            finally
            {
                await _conn.CloseAsync();
            }
            return profile;
        }

        public async Task<int> UpdateProfile(UserProfile profile)
        {
            int status = 0;
            // Only update: username, full_name, mobile, gender, profile_image
            var query = @"UPDATE t_user 
                            SET c_username       = @c_username,
                                c_full_name      = @c_full_name,
                                c_mobile         = @c_mobile,
                                c_gender         = @c_gender,
                                c_profile_image  = @c_profile_image
                            WHERE c_user_id = @c_user_id;";
            try
            {
                await _conn.CloseAsync();
                await _conn.OpenAsync();
                using (var cmd = new NpgsqlCommand(query, _conn))
                {
                    cmd.Parameters.AddWithValue("@c_user_id",       profile.c_UserId);
                    cmd.Parameters.AddWithValue("@c_username",      (object?)profile.c_UserName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@c_full_name",     (object?)profile.c_FullName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@c_mobile",        (object?)profile.c_Mobile   ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@c_gender",        (object?)profile.c_Gender   ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@c_profile_image", (object?)profile.c_Image    ?? DBNull.Value);

                    status = await cmd.ExecuteNonQueryAsync();
                    Console.WriteLine("Rows affected: " + status);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("UpdateProfile Error: " + ex.Message);
                status = -1;
            }
            finally
            {
                await _conn.CloseAsync();
            }
            return status;
        }

          public async Task<int> ChangePassword(int Userid, string oldPwd, string newPwd)
        {
            try
            {
                // 1. Connection state check (Safety)
                if (_conn.State != System.Data.ConnectionState.Open)
                {
                    await _conn.OpenAsync();
                }

                // 2. Fetch current hashed password
                var cmd = new NpgsqlCommand("SELECT c_password_hash FROM t_user WHERE c_user_id = @id", _conn);
                cmd.Parameters.AddWithValue("@id", Userid);

                var dbValue = await cmd.ExecuteScalarAsync();

                // Agar user ID galat hai ya password null hai
                if (dbValue == null || dbValue == DBNull.Value)
                {
                    return -1; // User not found
                }

                string currentHashed = dbValue.ToString();

                // 3. Verify Old Password (BCrypt check)
                // Ensure BCrypt.Net-Next NuGet package is installed
                bool isValid = BCrypt.Net.BCrypt.Verify(oldPwd, currentHashed);
                if (!isValid) return 0; // Password mismatch

                // 4. Update with New Hash
                string newHashed = BCrypt.Net.BCrypt.HashPassword(newPwd);
                var upCmd = new NpgsqlCommand("UPDATE t_user SET c_password_hash = @pwd WHERE c_user_id = @id", _conn);
                upCmd.Parameters.AddWithValue("@pwd", newHashed);
                upCmd.Parameters.AddWithValue("@id", Userid);

                int rowsAffected = await upCmd.ExecuteNonQueryAsync();
                return rowsAffected > 0 ? 1 : -1;
            }
            catch (Exception ex)
            {
                // Console pe check karein exact error kya hai
                Console.WriteLine("CRITICAL ERROR: " + ex.Message);
                throw; // Taaki API ko pata chale ki error aayi hai
            }
            finally
            {
                // Connection hamesha close hona chahiye
                if (_conn.State == System.Data.ConnectionState.Open)
                {
                    await _conn.CloseAsync();
                }
            }
        }

    }
}