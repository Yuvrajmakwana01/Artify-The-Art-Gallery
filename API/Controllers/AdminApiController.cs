using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;
using Repository.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminApiController : ControllerBase
    {
        private readonly IAuthInterface _repo;
        private readonly IConfiguration _config;

        public AdminApiController(IAuthInterface repo, IConfiguration config)
        {
            _repo = repo;
            _config = config;
        }

        // POST api/AdminApi/login
        // Called by the admin Login.cshtml AJAX request.
        // Returns a JWT on success — the MVC layer stores it in Session.
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] vm_AdminLogin admin)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid input." });

            var data = await _repo.AdminLogin(admin);

            if (data == null)
                return Ok(new { success = false, message = "Invalid email or password." });

            // ── Build JWT ────────────────────────────────────────────────────
            var jwtKey = _config["Jwt:Key"];
            var jwtIssuer = _config["Jwt:Issuer"];
            var jwtAudience = _config["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(jwtKey) || string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
                return StatusCode(500, new { success = false, message = "JWT configuration missing." });

            var claims = new[]
            {
                new Claim("AdminId",   data.c_AdminId.ToString()),
                new Claim("AdminName", data.c_AdminName),
                new Claim("Email",     data.c_Email)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            return Ok(new
            {
                success = true,
                token = new JwtSecurityTokenHandler().WriteToken(token),
                Admin = new
                {
                    data.c_AdminId,
                    data.c_AdminName,
                    data.c_Email
                }
            });
        }
    }
}
