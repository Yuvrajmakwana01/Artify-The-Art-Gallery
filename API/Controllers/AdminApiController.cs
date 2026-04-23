using Microsoft.AspNetCore.Mvc;
using Repository;
using Npgsql;
using Repository.Interfaces;
using Repository.Models;
using Repository.Services;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminApiController : ControllerBase
{
    private readonly IAdminInterface             _adminRepo;
    private readonly IAuthInterface              _repo;
    private readonly IConfiguration              _config;
    private readonly RedisService                _redis;
    private readonly ILogger<AdminApiController> _logger;

    public AdminApiController(
        IAuthInterface              repo,
        IConfiguration              config,
        IAdminInterface             adminRepo,
        RedisService                redis,
        ILogger<AdminApiController> logger)
    {
        _repo      = repo;
        _config    = config;
        _adminRepo = adminRepo;
        _redis     = redis;
        _logger    = logger;
    }
    // 1. Dashboard Summary
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            var data = await _adminRepo.GetAllDashboardInfo();
            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
    // 2. Revenue (WEEKLY / MONTHLY / YEARLY)
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue([FromQuery] string type)
    {
        try
        {
            var data = await _adminRepo.GetRevenue(type);
            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
    // 3. Users Count (WEEKLY / MONTHLY / YEARLY)
    [HttpGet("users-count")]
    public async Task<IActionResult> GetUsersCount([FromQuery] string type)
    {
        try
        {
            var data = await _adminRepo.GetUsersCount(type);
            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    // 4. Total Users & Artists
    [HttpGet("total-count")]
    public async Task<IActionResult> GetTotalUsersCount()
    {
        try
        {
            var data = await _adminRepo.GetTotalUsersCount();
            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
    // 5. Top Selling Category
    [HttpGet("top-category")]
    public async Task<IActionResult> GetTopSellingCategory()
    {
        try
        {
            var data = await _adminRepo.TopSellingCategory();
            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    // 6. Top Performing Artists
    [HttpGet("top-artists")]
    public async Task<IActionResult> GetTopArtists()
    {
        try
        {
            var data = await _adminRepo.TopPerformingArtist();
            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    // 7. Recent Activities
    [HttpGet("recent-activity")]
    public async Task<IActionResult> GetRecentActivity()
    {
        try
        {
            var data = await _adminRepo.RecentActivity();
            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    // POST api/AdminApi/login
    // Called by the admin Login.cshtml AJAX request.
    // Returns a JWT on success — the MVC layer stores it in Session.
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] vm_AdminLogin admin)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            return BadRequest(new
            {
                success = false,
                message = "Validation failed",
                errors = errors
            });
        }

        var data = await _repo.AdminLogin(admin);

        if (data == null)
        {
            _logger.LogWarning($"Failed admin login attempt: {admin.c_Email}");
            return Unauthorized(new
            {
                success = false,
                message = "Invalid credentials"
            });
        }
        _logger.LogInformation($"Admin logged in successfully: {admin.c_Email}");

        // ── Build JWT ────────────────────────────────────────────────────
        var jwtKey = _config["Jwt:Key"];
        var jwtIssuer = _config["Jwt:Issuer"];
        var jwtAudience = _config["Jwt:Audience"];

        if (string.IsNullOrWhiteSpace(jwtKey) || string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
            return StatusCode(500, new { success = false, message = "JWT configuration missing." });

        var claims = new[]
        {
            new Claim("AdminId", data.c_AdminId.ToString()),
            new Claim("AdminName", data.c_AdminName),
            new Claim("Email", data.c_Email),
            new Claim(ClaimTypes.Role, "Admin")
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

    // ══════════════════════════════════════════════════════════════
    //  ADMIN NOTIFICATION ENDPOINTS
    //  These read from Redis where RabbitService consumer deposited
    //  all messages sent to the "admin_notifications" queue.
    //  Redis key: notifications:admin:all
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the latest admin notifications (default: 20).
    /// Also returns the current unread badge count.
    /// </summary>
    [HttpGet("notifications")]
    public async Task<IActionResult> GetAdminNotifications([FromQuery] int take = 20)
    {
        if (take < 1)  take = 1;
        if (take > 50) take = 50;

        var data        = await _redis.GetNotificationsAsync("admin", "all", take);
        var unreadCount = await _redis.GetNotificationCountAsync("admin", "all");

        return Ok(new { success = true, unreadCount, data });
    }

    /// <summary>
    /// Marks ALL admin notifications as read (clears list + resets badge).
    /// Called when the admin clicks "Mark all read".
    /// </summary>
    [HttpPost("notifications/read")]
    public async Task<IActionResult> MarkAllAdminNotificationsRead()
    {
        await _redis.ClearNotificationsAsync("admin", "all");
        return Ok(new { success = true });
    }

    /// <summary>
    /// Marks a single notification as read by its ID.
    /// Decrements the unread badge count by 1.
    /// </summary>
    [HttpPost("notifications/{notificationId}/read")]
    public async Task<IActionResult> MarkAdminNotificationRead(
        [FromRoute] string notificationId)
    {
        if (string.IsNullOrWhiteSpace(notificationId))
            return BadRequest(new { success = false, message = "Notification id is required." });

        var removed = await _redis.MarkAsReadAsync("admin", "all", notificationId);

        if (!removed)
            return NotFound(new { success = false, message = "Notification not found." });

        var unreadCount = await _redis.GetNotificationCountAsync("admin", "all");
        return Ok(new { success = true, unreadCount });
    }
}