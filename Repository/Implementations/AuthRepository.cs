using System.Data;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Repository.Interfaces;
using Npgsql;
using Repository.Models;
using BCrypt.Net;
using Repository.Models;

namespace Repository.Implementations
{
    public class AuthRepository : IAuthInterface
    {
        private readonly NpgsqlConnection _conn;

        public AuthRepository(NpgsqlConnection conn)
        {
            _conn = conn;
        }
        // Admin Login ────────────────────────────────────────────────
        // Queries t_admin table, verifies BCrypt hash
        public async Task<t_Admin?> AdminLogin(vm_AdminLogin admin)
        {
            t_Admin? adminData = null;
            var qry = "SELECT * FROM t_admin WHERE c_email = @email";
            using (NpgsqlCommand com = new NpgsqlCommand(qry, _conn))
            {
                com.Parameters.AddWithValue("@email", admin.c_Email.Trim().ToLower());
                await _conn.OpenAsync();

                using (var reader = await com.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        string storedHash = reader["c_password"].ToString()!;

                        // BCrypt verify — same library used by ArtistRepository
                        if (BCrypt.Net.BCrypt.Verify(admin.c_Password, storedHash))
                        {
                            adminData = new t_Admin
                            {
                                c_AdminId = Convert.ToInt32(reader["c_adminid"]),
                                c_AdminName = reader["c_adminname"].ToString()!,
                                c_Email = reader["c_email"].ToString()!
                            };
                        }
                    }
                }
            }
            return adminData;
        }


        private string HashPassword(string password)
        {
            using SHA256 sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public async Task<int> UserRegister(t_UserRegister model)
        {
            try
            {
                await _conn.OpenAsync();


                var emailCheckQry = @"SELECT COUNT(1) FROM t_user WHERE LOWER(c_email) = LOWER(@email)";
                using (var emailCmd = new NpgsqlCommand(emailCheckQry, _conn))
                {
                    emailCmd.Parameters.AddWithValue("@email", model.c_Email.Trim());
                    var emailCount = Convert.ToInt64(await emailCmd.ExecuteScalarAsync());
                    if (emailCount > 0)
                        return 0;
                }

                var usernameCheckQry = @"SELECT COUNT(1) FROM t_user WHERE LOWER(c_username) = LOWER(@username)";
                using (var userCmd = new NpgsqlCommand(usernameCheckQry, _conn))
                {
                    userCmd.Parameters.AddWithValue("@username", model.c_UserName.Trim());
                    var userCount = Convert.ToInt64(await userCmd.ExecuteScalarAsync());
                    if (userCount > 0)
                        return -1;
                }

                var insertQry = @"
                    INSERT INTO t_user(c_full_name, c_username, c_email, c_password_hash, c_gender, c_mobile, c_profile_image)
                    VALUES (@fullName, @username, @email, @passwordHash, @gender, @mobile, @profileImage)";

                using (var insertCmd = new NpgsqlCommand(insertQry, _conn))
                {
                    insertCmd.Parameters.AddWithValue("@fullName", model.c_FullName.Trim());
                    insertCmd.Parameters.AddWithValue("@username", model.c_UserName.Trim());
                    insertCmd.Parameters.AddWithValue("@email", model.c_Email.ToLower().Trim());
                    insertCmd.Parameters.AddWithValue("@passwordHash", model.c_PasswordHash);
                    insertCmd.Parameters.AddWithValue("@gender", model.c_Gender);
                    insertCmd.Parameters.AddWithValue("@mobile", string.IsNullOrEmpty(model.c_Mobile) ? DBNull.Value : (object)model.c_Mobile);
                    insertCmd.Parameters.AddWithValue("@profileImage", string.IsNullOrEmpty(model.c_ProfileImage) ? DBNull.Value : (object)model.c_ProfileImage);

                    var result = await insertCmd.ExecuteNonQueryAsync();
                    return result; // returns 1
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Admin Login Error: " + ex.Message);
                Console.WriteLine("Error in user register:" + ex.Message);
                return -99;
            }
            finally
            {
                await _conn.CloseAsync();
            }


        }

        public async Task<t_UserRegister?> UserLogin(t_UserLogin model)
{
    t_UserRegister user = null;

    try
    {
        if (_conn.State == ConnectionState.Closed)
            await _conn.OpenAsync();

        var qry = @"SELECT c_user_id, c_full_name, c_username, c_email, c_password_hash, c_gender, c_mobile, c_profile_image
                    FROM t_user WHERE LOWER(c_email) = @email";

        using (var cmd = new NpgsqlCommand(qry, _conn))
        {
            cmd.Parameters.AddWithValue("@email", model.c_Email.Trim().ToLower());

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            user = new t_UserRegister
            {
                c_UserId = Convert.ToInt32(reader["c_user_id"]),
                c_FullName = reader["c_full_name"].ToString(),
                c_UserName = reader["c_username"].ToString(),
                c_Email = reader["c_email"].ToString(),
                c_PasswordHash = reader["c_password_hash"]?.ToString(),
                c_Gender = reader["c_gender"].ToString(),
                c_Mobile = reader["c_mobile"].ToString(),
                c_ProfileImage = reader["c_profile_image"] == DBNull.Value ? null : reader["c_profile_image"].ToString()
            };
        }

        return user;
    }
    finally
    {
        await _conn.CloseAsync();
    }
}

        public async Task<t_UserRegister?> GetUserByEmail(string email)
        {
            t_UserRegister user = new t_UserRegister();

            try
            {
                await _conn.OpenAsync();

                var qry = @"
                    SELECT c_user_id, c_full_name, c_username, c_email, c_password_hash, c_gender, c_mobile, c_profile_image
                    FROM t_user WHERE LOWER(c_email) = LOWER(@email)";

                using var cmd = new NpgsqlCommand(qry, _conn);
                cmd.Parameters.AddWithValue("@email", email.Trim());

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                user.c_UserId = Convert.ToInt32(reader["c_user_id"]);
                user.c_FullName = reader["c_full_name"].ToString();
                user.c_UserName = reader["c_username"].ToString();
                user.c_Email = reader["c_email"].ToString();
                user.c_PasswordHash = reader["c_password_hash"].ToString();
                user.c_Gender = reader["c_gender"].ToString();
                user.c_Mobile = reader["c_mobile"].ToString();
                user.c_ProfileImage = reader["c_profile_image"] == DBNull.Value ? null : reader["c_profile_image"].ToString();

                return user;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in get user by email:", ex.Message);
                return null;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }

        public async Task<int> UpdatePassword(string email, string newPassword)
        {
            try
            {
                if (_conn.State != System.Data.ConnectionState.Open)
                    await _conn.OpenAsync();

                // var hashedPassword = HashPassword(newPassword);

                var qry = @"
                    UPDATE t_user
                    SET c_password_hash = @passwordHash
                    WHERE LOWER(c_email) = LOWER(@email)";

                using var cmd = new NpgsqlCommand(qry, _conn);
                cmd.Parameters.AddWithValue("@passwordHash", newPassword);
                cmd.Parameters.AddWithValue("@email", email.Trim());

                return await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in update password:", ex.Message);
                return -99;
            }
            finally
            {
                await _conn.CloseAsync();
            }
        }
    }
}
