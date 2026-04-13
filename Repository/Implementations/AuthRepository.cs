using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repository.Interfaces;
using Npgsql;
using Repository.Models;
using BCrypt.Net;

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

            try
            {
                await _conn.OpenAsync();

                using (NpgsqlCommand com = new NpgsqlCommand(qry, _conn))
                {
                    com.Parameters.AddWithValue("@email", admin.c_Email.Trim().ToLower());

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
            }
            catch (Exception ex)
            {
                Console.WriteLine("Admin Login Error: " + ex.Message);
            }
            finally
            {
                await _conn.CloseAsync();
            }

            return adminData;
        }
    }
}

