using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Repository.Interfaces;
using Repository.Models;
using Repository.Interfaces;
using Repository.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminApiController : ControllerBase
{
    private readonly IAdmincategoiresInteface _categoryRepository;
    private readonly IAdminUsersInterface _userRepository;
    private readonly IAdminArtistInterface _artistRepository;

    public AdminApiController(
        IAdmincategoiresInteface categoryRepository,
        IAdminUsersInterface userRepository,
        IAdminArtistInterface artistRepository)
    {
        _categoryRepository = categoryRepository;
        _userRepository = userRepository;
        _artistRepository = artistRepository;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromQuery] string? search, [FromQuery] string? status)
    {
        var data = await _categoryRepository.GetCategoriesAsync(search, status);
        return Ok(data);
    }

    [HttpGet("categories/stats")]
    public async Task<IActionResult> GetCategoryStats()
    {
        var data = await _categoryRepository.GetCategoryStatsAsync();
        return Ok(data);
    }

    [HttpGet("categories/{id:int}")]
    public async Task<IActionResult> GetCategoryById(int id)
    {
        var data = await _categoryRepository.GetCategoryByIdAsync(id);
        return data is null ? NotFound() : Ok(data);
    }

    [HttpPost("categories")]
    public async Task<IActionResult> AddCategory([FromBody] AdminCategoryUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CategoryName))
            return BadRequest("Category name is required.");

        var id = await _categoryRepository.AddCategoryAsync(request);
        var item = await _categoryRepository.GetCategoryByIdAsync(id);
        return CreatedAtAction(nameof(GetCategoryById), new { id }, item);
    }

    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] AdminCategoryUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CategoryName))
            return BadRequest("Category name is required.");

        var ok = await _categoryRepository.UpdateCategoryAsync(id, request);
        return ok ? Ok(new { message = "Category updated successfully." }) : NotFound();
    }

    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        try
        {
            var ok = await _categoryRepository.DeleteCategoryAsync(id);
            return ok ? Ok(new { message = "Category deleted successfully." }) : NotFound();
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            return Conflict("Cannot delete this category because artworks are linked to it.");
        }
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] string? search)
    {
        var data = await _userRepository.GetUsersAsync(search);
        return Ok(data);
    }

    [HttpGet("users/stats")]
    public async Task<IActionResult> GetUserStats()
    {
        var data = await _userRepository.GetUserStatsAsync();
        return Ok(data);
    }

    [HttpGet("users/{id:int}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        var data = await _userRepository.GetUserByIdAsync(id);
        return data is null ? NotFound() : Ok(data);
    }

    [HttpPut("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] AdminUserUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Gender))
        {
            return BadRequest("FullName, Email, Username and Gender are required.");
        }

        var ok = await _userRepository.UpdateUserAsync(id, request);
        return ok ? Ok(new { message = "User updated successfully." }) : NotFound();
    }

    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            var ok = await _userRepository.DeleteUserAsync(id);
            return ok ? Ok(new { message = "User deleted successfully." }) : NotFound();
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            return Conflict("Cannot delete this user because related records exist.");
        }
    }

    [HttpGet("artists")]
    public async Task<IActionResult> GetArtists([FromQuery] string? search, [FromQuery] string? status)
    {
        var data = await _artistRepository.GetArtistsAsync(search, status);
        return Ok(data);
    }

    [HttpGet("artists/stats")]
    public async Task<IActionResult> GetArtistStats()
    {
        var data = await _artistRepository.GetArtistStatsAsync();
        return Ok(data);
    }

    [HttpGet("artists/{id:int}")]
    public async Task<IActionResult> GetArtistById(int id)
    {
        var data = await _artistRepository.GetArtistByIdAsync(id);
        return data is null ? NotFound() : Ok(data);
    }

    [HttpPost("artists")]
    public async Task<IActionResult> AddArtist([FromBody] AdminArtistUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ArtistName) || string.IsNullOrWhiteSpace(request.ArtistEmail))
            return BadRequest("ArtistName and ArtistEmail are required.");

        var id = await _artistRepository.AddArtistAsync(request);
        var item = await _artistRepository.GetArtistByIdAsync(id);
        return CreatedAtAction(nameof(GetArtistById), new { id }, item);
    }

    [HttpPut("artists/{id:int}")]
    public async Task<IActionResult> UpdateArtist(int id, [FromBody] AdminArtistUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ArtistName) || string.IsNullOrWhiteSpace(request.ArtistEmail))
            return BadRequest("ArtistName and ArtistEmail are required.");

        var ok = await _artistRepository.UpdateArtistAsync(id, request);
        return ok ? Ok(new { message = "Artist updated successfully." }) : NotFound();
    }

    [HttpDelete("artists/{id:int}")]
    public async Task<IActionResult> DeleteArtist(int id)
    {
        try
        {
            var ok = await _artistRepository.DeleteArtistAsync(id);
            return ok ? Ok(new { message = "Artist deleted successfully." }) : NotFound();
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            return Conflict("Cannot delete this artist because related records exist.");
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
