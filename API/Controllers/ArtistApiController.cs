using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Repository.Interfaces;
using Repository.Models;
using Repository.Services;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArtistApiController : ControllerBase
{
    private readonly IArtistInterface _repo;
    private readonly IArtworkInterface _artworkRepo;
    private readonly IConfiguration _config;
    private readonly RedisService _redis;

    private readonly EmailService _emailService;

    private readonly RabbitService _rabbit;
    private readonly ILogger<ArtistApiController> _logger;
    private readonly Cloudinary _cloudinary;

    public ArtistApiController(
        IArtistInterface repo,
        IArtworkInterface artworkRepo,
        IConfiguration config,
        EmailService emailService,
        RedisService redis,
        RabbitService rabbit,
        ILogger<ArtistApiController> logger)
    {
        _repo = repo;
        _artworkRepo = artworkRepo;
        _config = config;
        _redis = redis;
        _rabbit = rabbit;
        _emailService = emailService;
        _logger = logger;

        var cloudName = config["CloudinarySettings:CloudName"]
            ?? throw new InvalidOperationException("Cloudinary CloudName missing.");
        _cloudinary = new Cloudinary(new Account(
            cloudName,
            config["CloudinarySettings:ApiKey"],
            config["CloudinarySettings:ApiSecret"]));
    }

    // ── REGISTER ─────────────────────────────────────────────
    [HttpPost("Register")]
    public async Task<IActionResult> Register([FromForm] t_Artist user)
    {
        if (user.ProfilePicture != null)
        {
            using var stream = user.ProfilePicture.OpenReadStream();
            var up = await _cloudinary.UploadAsync(new ImageUploadParams
            {
                File = new FileDescription(user.ProfilePicture.FileName, stream),
                Folder = "Artify_Profiles",
                Transformation = new Transformation().Width(500).Height(500).Crop("fill")
            });
            user.c_Profile_Image = up.SecureUrl?.ToString();
        }

        var rows = await _repo.Register(user);

        // In the Register method, replace the email section with:

        if (rows > 0)
        {
            try
            {
                string loginUrl = _config["AppBaseUrl"] + "/Artist/Login";
                string logoPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "images", "Artify-Logos.png"));

                var placeholders = new Dictionary<string, string>
                {
                    { "ArtistName", user.c_Full_Name ?? user.c_UserName },
                    { "LoginUrl", loginUrl }
                };

                await _emailService.SendEmailAsync(
                    toEmail: user.c_Email,
                    subject: "Welcome to Artify - Artist Registration Received",
                    templateFile: "RegisterEmail.html",  // Use the existing template
                    placeholders: placeholders,
                    logoPath: logoPath
                );

                _logger.LogInformation("Welcome email sent successfully to {Email}", user.c_Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Artist welcome email failed for {Email}", user.c_Email);
                // Don't fail registration if email fails
            }

            return Ok(new { success = true, message = "Registered! Awaiting admin approval." });
        }

        return BadRequest(new { success = false, message = "Email already registered." });
    }

    // ── LOGIN ─────────────────────────────────────────────────
    // Issues JWT in response body (stored in localStorage by JS)
    // AND writes it to an HttpOnly cookie (used by server-side guard).
    [HttpPost("Login")]
    public async Task<IActionResult> Login([FromForm] vm_Login login)
    {
        var user = await _repo.Login(login);

        if (user == null)
            return Ok(new { success = false, message = "Invalid email or password." });

        if (user.c_Is_Blocked)
            return Ok(new
            {
                success = false,
                blocked = true,
                message = "Your account is temporarily inactive for 1 day because more than 3 artworks were rejected."
            });

        if (!user.c_Is_Active)
            return Ok(new
            {
                success = false,
                inactive = true,
                message = "Your account is pending admin approval."
            });

        var token = GenerateJwtToken(user);

        // ── Write JWT to HttpOnly cookie ──────────────────────
        // SameSite=None is required when API (5183) and MVC (5092)
        // run on different origins. In production with HTTPS,
        // Secure must also be true. For local HTTP dev, browsers
        // still accept it without Secure (Chrome/Edge allow this
        // on localhost specifically).
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.None,   // ← cross-origin cookie delivery
            Secure = Request.IsHttps,     // true in prod HTTPS; false on HTTP dev
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            Path = "/"
        };

        // Development fallback: if not HTTPS, set Secure=false explicitly
        // (Remove this block when deploying to HTTPS production)
        if (!Request.IsHttps)
        {
            cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,   // Lax works fine for same-site HTTP dev
                Secure = false,
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                Path = "/"
            };
        }

        Response.Cookies.Append("ArtistToken", token, cookieOptions);

        // ── Store identity in session (MVC controller guard) ──
        // HttpContext.Session.SetInt32("ArtistId",     user.c_User_Id);
        // HttpContext.Session.SetString("ArtistEmail",  user.c_Email);
        // HttpContext.Session.SetString("ArtistName",   user.c_Full_Name ?? user.c_UserName);

        return Ok(new
        {
            success = true,
            message = "Login successful.",
            token = token,
            userData = new
            {
                user.c_User_Id,
                user.c_UserName,
                user.c_Email,
                user.c_Full_Name,
                user.c_Profile_Image,
                user.c_Is_Active
            }
        });
    }


    // ── UPLOAD ARTWORK ─────────────────────────────────────────
    [Authorize]
    [HttpPost("Upload")]
    public async Task<IActionResult> Upload([FromForm] t_Artwork art)
    {
        if (art.ArtworkFile == null || art.ArtworkFile.Length == 0)
        {
            return BadRequest("Please select an image to upload.");
        }

        var account = new Account(
            _config["CloudinarySettings:CloudName"],
            _config["CloudinarySettings:ApiKey"],
            _config["CloudinarySettings:ApiSecret"]
        );

        var cloudinary = new Cloudinary(account);

        try
        {
            using (var stream = art.ArtworkFile.OpenReadStream())
            {
                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(art.ArtworkFile.FileName, stream),
                    Folder = "Artify_Gallery",
                    UploadPreset = _config["CloudinarySettings:UploadPreset"] ?? "Cloudinary_Setup"
                };

                var uploadResult = await cloudinary.UploadAsync(uploadParams);

                if (uploadResult.Error != null)
                    return BadRequest(uploadResult.Error.Message);

                art.c_original_path = uploadResult.SecureUrl.ToString();

                // ============================================================
                // WATERMARK CONFIGURATION - Small & Visible Only on Zoom
                // ============================================================
                // 
                // Design approach:
                // 1. Very low opacity (8-12%) so it blends with image
                // 2. Small font size (proportional to image)
                // 3. Positioned in corner (bottom-right)
                // 4. Becomes visible when user zooms in due to rasterization
                // 5. Uses subtle color that complements the image
                // ============================================================

                art.c_preview_path = cloudinary.Api.UrlImgUp.Transform(new Transformation()
                    .Width(800).Crop("scale").Quality("auto")
                    // Subtle watermark that appears only on zoom
                    // Position: bottom-right corner, small size, very low opacity
                    .Overlay(new TextLayer()
                        .Text("© Artify")
                        .FontFamily("Arial")
                        .FontSize(24)           // Small size - barely visible at normal view
                        .FontWeight("normal")
                        .TextAlign("right"))
                    .Gravity("south_east")       // Position at bottom-right corner
                    .X(15)                       // 15px margin from edge
                    .Y(15)                       // 15px margin from bottom
                    .Opacity(12)                 // 12% opacity - very subtle, visible only on zoom
                    .Color("#FFFFFF")             // White color blends with most images
                    .Chain())
                    .BuildUrl(uploadResult.PublicId);
            }

            var status = await _artworkRepo.UploadArtwork(art);
            return status > 0 ? Ok(new { success = true, message = "Masterpiece uploaded!" })
                              : BadRequest("Failed to save artwork to database.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    // ── GET ALL ARTWORKS ───────────────────────────────────────
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        var artworks = await _artworkRepo.GetAllArtworks();
        return Ok(new { success = true, data = artworks });
    }

    // ── GET MY ARTWORKS — artist sees only their own ──────────
    // artistId is read from JWT claim, NOT from URL → prevents IDOR
    [Authorize]
    [HttpGet("GetMyArtworks")]
    public async Task<IActionResult> GetMyArtworks()
    {
        var artistId = GetArtistIdFromToken();
        if (artistId == 0) return Unauthorized(new { message = "Invalid token." });
        var artworks = await _artworkRepo.GetArtworksByArtist(artistId);
        return Ok(new { success = true, data = artworks });
    }

    // ── GET BY ARTIST (backward compat, IDOR-protected) ───────
    // Notifications for the signed-in artist, backed by Redis.
    [Authorize]
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] int take = 20)
    {
        var artistId = GetArtistIdFromToken();
        if (artistId == 0) return Unauthorized(new { message = "Invalid token." });

        if (take < 1) take = 1;
        if (take > 50) take = 50;

        var recipientId = artistId.ToString();
        var data = await _redis.GetNotificationsAsync("artist", recipientId, take);
        var unreadCount = await _redis.GetNotificationCountAsync("artist", recipientId);

        return Ok(new
        {
            success = true,
            unreadCount,
            data
        });
    }

    [Authorize]
    [HttpPost("notifications/read")]
    public async Task<IActionResult> MarkNotificationsRead()
    {
        var artistId = GetArtistIdFromToken();
        if (artistId == 0) return Unauthorized(new { message = "Invalid token." });

        await _redis.ClearNotificationsAsync("artist", artistId.ToString());
        return Ok(new { success = true });
    }

    [Authorize]
    [HttpPost("notifications/{notificationId}/read")]
    public async Task<IActionResult> MarkNotificationRead([FromRoute] string notificationId)
    {
        var artistId = GetArtistIdFromToken();
        if (artistId == 0) return Unauthorized(new { message = "Invalid token." });

        if (string.IsNullOrWhiteSpace(notificationId))
            return BadRequest(new { success = false, message = "Notification id is required." });

        var recipientId = artistId.ToString();
        var removed = await _redis.MarkAsReadAsync("artist", recipientId, notificationId);

        if (!removed)
            return NotFound(new { success = false, message = "Notification not found." });

        var unreadCount = await _redis.GetNotificationCountAsync("artist", recipientId);

        return Ok(new
        {
            success = true,
            unreadCount
        });
    }

    [Authorize]
    [HttpGet("GetByArtist/{id}")]
    public async Task<IActionResult> GetByArtist(int id)
    {
        if (GetArtistIdFromToken() != id) return Forbid();
        return Ok(await _artworkRepo.GetArtworksByArtist(id));
    }

    // ── CATEGORIES (public) ────────────────────────────────────
    [HttpGet("GetCategories")]
    public async Task<IActionResult> GetCategories()
        => Ok(await _artworkRepo.GetCategories());

    // ── GET BY ID ─────────────────────────────────────────────
    [HttpGet("GetById/{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var art = await _artworkRepo.GetById(id);
        return art == null
            ? NotFound(new { message = $"Artwork {id} not found." })
            : Ok(art);
    }

    // ── APPROVED (public) ─────────────────────────────────────
    [HttpGet("GetApproved")]
    public async Task<IActionResult> GetApproved()
        => Ok(await _artworkRepo.GetApprovedArtworks());

    // ── DELETE ────────────────────────────────────────────────
    [Authorize]
    [HttpDelete("Delete/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var rows = await _artworkRepo.DeleteArtwork(id);
        return rows > 0
            ? Ok(new { success = true, message = "Artwork deleted." })
            : NotFound(new { success = false, message = "Artwork not found." });
    }


    [Authorize]
    [HttpPut("Update")]
    public async Task<IActionResult> Update([FromForm] t_Artwork art)
    {
        try
        {
            if (art.ArtworkFile != null && art.ArtworkFile.Length > 0)
            {
                // New image uploaded - apply fresh watermark
                using var stream = art.ArtworkFile.OpenReadStream();
                var result = await _cloudinary.UploadAsync(new ImageUploadParams
                {
                    File = new FileDescription(art.ArtworkFile.FileName, stream),
                    Folder = "Artify_Gallery",
                    UploadPreset = _config["CloudinarySettings:UploadPreset"] ?? "Cloudinary_Setup"
                });

                art.c_original_path = result.SecureUrl.ToString();
                art.c_preview_path = _cloudinary.Api.UrlImgUp
                    .Transform(new Transformation()
                        .Width(800).Crop("scale").Quality("auto")
                        .Overlay(new TextLayer()
                            .Text("© Artify")
                            .FontFamily("Arial")
                            .FontSize(24)
                            .FontWeight("normal")
                            .TextAlign("right"))
                        .Gravity("south_east")
                        .X(15)
                        .Y(15)
                        .Opacity(12)
                        .Color("#FFFFFF")
                        .Chain())
                    .BuildUrl(result.PublicId);
            }
            else
            {
                // No new image - keep existing paths from database
                var existingArtwork = await _artworkRepo.GetById(art.c_artwork_id);
                if (existingArtwork != null)
                {
                    art.c_original_path = existingArtwork.c_original_path;
                    art.c_preview_path = existingArtwork.c_preview_path;
                }
            }

            var rows = await _artworkRepo.UpdateArtwork(art);
            return rows > 0
                ? Ok(new { success = true, message = "Artwork updated successfully!" })
                : NotFound(new { success = false, message = "Artwork not found." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // ── DASHBOARD ─────────────────────────────────────────────
    [Authorize]
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var artistId = GetArtistIdFromToken();
        if (artistId == 0) return Unauthorized(new { message = "Invalid token." });
        var data = await _repo.GetDashboardData(artistId);
        return data == null ? NotFound() : Ok(data);
    }

    // ── CHANGE PASSWORD ────────────────────────────────────────
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] t_ChangePassword model)
    {
        if (GetArtistIdFromToken() != model.c_Artist_Id) return Forbid();
        var result = await _repo.ChangePassword(
            model.c_Artist_Id, model.c_CurrentPassword, model.c_NewPassword);
        if (result == 1) return Ok(new { success = true, message = "Password updated." });
        if (result == 0) return Ok(new { success = false, message = "Incorrect current password." });
        return BadRequest(new { success = false, message = "Update failed." });
    }

    // ── PROFILE ────────────────────────────────────────────────
    [Authorize]
    [HttpGet("profile/{id}")]
    public async Task<IActionResult> GetProfile(int id)
    {
        if (GetArtistIdFromToken() != id) return Forbid();
        return Ok(await _repo.GetArtistById(id));
    }

    [Authorize]
    [HttpPost("profile")]
    public async Task<IActionResult> EditProfile([FromForm] t_ArtistProfile model)
    {
        if (GetArtistIdFromToken() != model.ArtistId) return Forbid();

        // ── Handle Cover Image ──────────────────────────────────
        if (model.CoverImageFile != null)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(model.CoverImageFile.FileName);
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(),
                "..", "MVC", "wwwroot", "Cover_Images");
            Directory.CreateDirectory(folderPath);
            await using var stream = new FileStream(
                Path.Combine(folderPath, fileName), FileMode.Create);
            await model.CoverImageFile.CopyToAsync(stream);
            model.CoverImage = fileName;
        }

        // ── Handle Profile Picture ──────────────────────────────
        if (model.ProfilePictureFile != null)
        {
            var picName = Guid.NewGuid() + Path.GetExtension(model.ProfilePictureFile.FileName);
            var picFolder = Path.Combine(Directory.GetCurrentDirectory(),
                "..", "MVC", "wwwroot", "Profile_Images");
            Directory.CreateDirectory(picFolder);
            await using var picStream = new FileStream(
                Path.Combine(picFolder, picName), FileMode.Create);
            await model.ProfilePictureFile.CopyToAsync(picStream);
            model.ProfilePicture = picName;
        }

        var result = await _repo.EditArtistProfile(model);
        return Ok(new { success = result == 1, rows = result });
    }

    // ── REVENUE ────────────────────────────────────────────────
    [Authorize]
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue()
    {
        var id = GetArtistIdFromToken();
        if (id == 0) return Unauthorized();
        return Ok(await _repo.GetMonthlyRevenue(id));
    }

    // ── CATEGORY SALES ─────────────────────────────────────────
    [Authorize]
    [HttpGet("category-sales")]
    public async Task<IActionResult> GetCategorySales()
    {
        var id = GetArtistIdFromToken();
        if (id == 0) return Unauthorized();
        return Ok(await _repo.GetSalesByCategory(id));
    }

    // ── EARNINGS SUMMARY ───────────────────────────────────────
    [Authorize]
    [HttpGet("earnings-summary")]
    public async Task<IActionResult> GetEarningsSummary()
    {
        var id = GetArtistIdFromToken();
        if (id == 0) return Unauthorized();
        return Ok(await _repo.GetEarningsSummary(id));
    }

    // ── LOGOUT ────────────────────────────────────────────────
    [HttpPost("Logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("ArtistToken",
            new CookieOptions { SameSite = SameSiteMode.None, Secure = Request.IsHttps });
        HttpContext.Session.Clear();
        return Ok(new { success = true, message = "Logged out." });
    }

    // ── DEACTIVATE ACCOUNT ────────────────────────────────────
    // Sets c_is_active = false; clears auth cookie so the artist is
    // immediately signed out. Admin can reactivate from the admin panel.
    [Authorize]
    [HttpPost("deactivate")]
    public async Task<IActionResult> DeactivateAccount()
    {
        var artistId = GetArtistIdFromToken();
        if (artistId == 0) return Unauthorized(new { message = "Invalid token." });

        var result = await _repo.DeactivateAccount(artistId);

        if (result < 0)
            return StatusCode(500, new { success = false, message = "Deactivation failed." });

        // Clear auth cookie so the browser session ends immediately
        Response.Cookies.Delete("ArtistToken",
            new CookieOptions { SameSite = SameSiteMode.None, Secure = Request.IsHttps });
        HttpContext.Session.Clear();

        return Ok(new { success = true, message = "Account deactivated. You have been logged out." });
    }

    // ── PRIVATE HELPERS ───────────────────────────────────────
    private int GetArtistIdFromToken()
    {
        var claim = User.FindFirst("UserId");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }

    private string GenerateJwtToken(t_Artist user)
    {
        var key = _config["Jwt:Key"]!;
        if (key.Length < 32)
            throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.c_Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("UserId",   user.c_User_Id.ToString()),
            new Claim("UserName", user.c_UserName),
            new Claim("Email",    user.c_Email)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ── ARTIST PAYOUT HISTORY (approved only) ─────────────────
    [Authorize]
    [HttpGet("my-payout-history")]
    public async Task<IActionResult> GetMyPayoutHistory()
    {
        var id = GetArtistIdFromToken();
        if (id == 0) return Unauthorized(new { message = "Invalid token." });
        var data = await _repo.GetApprovedPayoutHistory(id);
        return Ok(data);
    }

    // ── ARTIST TRANSACTION LOGS (all buyer purchases) ──────────
    [Authorize]
    [HttpGet("my-transaction-logs")]
    public async Task<IActionResult> GetMyTransactionLogs()
    {
        var id = GetArtistIdFromToken();
        if (id == 0) return Unauthorized(new { message = "Invalid token." });
        var data = await _repo.GetTransactionLogs(id);
        return Ok(data);
    }
}
