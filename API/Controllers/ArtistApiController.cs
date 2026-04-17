// // ============================================================
// //  API/Controllers/ArtistApiController.cs
// //  All protected endpoints use [Authorize] with JWT Bearer.
// //  Login issues both a JWT *and* sets an HttpOnly cookie so
// //  MVC pages can authenticate server-side via the cookie.
// // ============================================================

// using Microsoft.AspNetCore.Authentication.Cookies;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.IdentityModel.Tokens;
// using System.IdentityModel.Tokens.Jwt;
// using System.Security.Claims;
// using System.Text;
// using CloudinaryDotNet;
// using CloudinaryDotNet.Actions;
// using Repository.Interfaces;
// using Repository.Models;

// namespace API.Controllers;

// [ApiController]
// [Route("api/[controller]")]
// public class ArtistApiController : ControllerBase
// {
//     private readonly IArtistInterface _repo;
//     private readonly IArtworkInterface _artworkRepo;
//     private readonly IConfiguration _config;
//     private readonly Cloudinary _cloudinary;

//     public ArtistApiController(
//         IArtistInterface repo,
//         IArtworkInterface artworkRepo,
//         IConfiguration config)
//     {
//         _repo = repo;
//         _artworkRepo = artworkRepo;
//         _config = config;

//         var cloudName = config["CloudinarySettings:CloudName"]
//             ?? throw new InvalidOperationException("Cloudinary CloudName missing.");
//         _cloudinary = new Cloudinary(new Account(
//             cloudName,
//             config["CloudinarySettings:ApiKey"],
//             config["CloudinarySettings:ApiSecret"]));
//     }

//     // ── REGISTER ─────────────────────────────────────────────
//     [HttpPost("Register")]
//     public async Task<IActionResult> Register([FromForm] t_Artist user)
//     {
//         if (user.ProfilePicture != null)
//         {
//             using var stream = user.ProfilePicture.OpenReadStream();
//             var up = await _cloudinary.UploadAsync(new ImageUploadParams
//             {
//                 File = new FileDescription(user.ProfilePicture.FileName, stream),
//                 Folder = "Artify_Profiles",
//                 Transformation = new Transformation().Width(500).Height(500).Crop("fill")
//             });
//             user.c_Profile_Image = up.SecureUrl?.ToString();
//         }

//         var rows = await _repo.Register(user);
//         return rows > 0
//             ? Ok(new { success = true, message = "Registered successfully. Awaiting admin approval." })
//             : BadRequest(new { success = false, message = "Email already registered." });
//     }

//     // ── LOGIN — issues JWT + HttpOnly cookie ──────────────────
//     [HttpPost("Login")]
//     public async Task<IActionResult> Login([FromForm] vm_Login login)
//     {
//         var user = await _repo.Login(login);

//         if (user == null)
//             return Ok(new { success = false, message = "Invalid email or password." });

//         if (!user.c_Is_Active)
//             return Ok(new
//             {
//                 success = false,
//                 inactive = true,
//                 message = "Your account is awaiting admin approval."
//             });

//         var token = GenerateJwtToken(user);

//         // ── Store JWT in HttpOnly cookie so MVC server-side guard works ──
//         Response.Cookies.Append("ArtistToken", token, new CookieOptions
//         {
//             HttpOnly = true,
//             Secure = Request.IsHttps,
//             SameSite = SameSiteMode.Lax,
//             Expires = DateTimeOffset.UtcNow.AddDays(7)
//         });

//         // ── Also store ArtistId in Session (used by MVC controller guard) ──
//         HttpContext.Session.SetInt32("ArtistId", user.c_User_Id);
//         HttpContext.Session.SetString("ArtistEmail", user.c_Email);
//         HttpContext.Session.SetString("ArtistName", user.c_Full_Name);

//         return Ok(new
//         {
//             success = true,
//             message = "Login successful.",
//             token = token,         // returned to JS → stored in localStorage
//             userData = new            // safe subset for localStorage
//             {
//                 user.c_User_Id,
//                 user.c_UserName,
//                 user.c_Email,
//                 user.c_Full_Name,
//                 user.c_Profile_Image,
//                 user.c_Is_Active
//             }
//         });
//     }

//     // ── UPLOAD ARTWORK ─────────────────────────────────────────
//     [Authorize]
//     [HttpPost("Upload")]
//     public async Task<IActionResult> Upload([FromForm] t_Artwork art)
//     {
//         if (art.ArtworkFile == null || art.ArtworkFile.Length == 0)
//             return BadRequest(new { success = false, message = "Please select an image." });

//         try
//         {
//             using var stream = art.ArtworkFile.OpenReadStream();
//             var uploadResult = await _cloudinary.UploadAsync(new ImageUploadParams
//             {
//                 File = new FileDescription(art.ArtworkFile.FileName, stream),
//                 Folder = "Artify_Gallery",
//                 UploadPreset = _config["CloudinarySettings:UploadPreset"] ?? "Cloudinary_Setup"
//             });

//             if (uploadResult.Error != null)
//                 return BadRequest(new { success = false, message = uploadResult.Error.Message });

//             art.c_original_path = uploadResult.SecureUrl.ToString();
//             art.c_preview_path = _cloudinary.Api.UrlImgUp
//                 .Transform(new Transformation()
//                     .Width(800).Crop("scale").Quality("auto")
//                     .Overlay(new TextLayer().Text("Artify").FontFamily("Arial").FontSize(60).FontWeight("bold"))
//                     .Opacity(30).Chain())
//                 .BuildUrl(uploadResult.PublicId);

//             var rows = await _artworkRepo.UploadArtwork(art);
//             return rows > 0
//                 ? Ok(new { success = true, message = "Masterpiece uploaded and submitted for review!" })
//                 : BadRequest(new { success = false, message = "Failed to save artwork." });
//         }
//         catch (Exception ex)
//         {
//             return StatusCode(500, new { success = false, message = ex.Message });
//         }
//     }

//     // ── GET ALL ARTWORKS ───────────────────────────────────────
//     [HttpGet("GetAll")]
//     public async Task<IActionResult> GetAll()
//     {
//         var artworks = await _artworkRepo.GetAllArtworks();
//         return Ok(new { success = true, data = artworks });
//     }

//     // ── GET ARTWORKS FOR THIS ARTIST ONLY ─────────────────────
//     // Uses JWT claim — no artistId in URL (prevents IDOR)
//     [Authorize]
//     [HttpGet("GetMyArtworks")]
//     public async Task<IActionResult> GetMyArtworks()
//     {
//         var artistId = GetArtistIdFromToken();
//         if (artistId == 0) return Unauthorized(new { message = "Invalid token." });

//         var artworks = await _artworkRepo.GetArtworksByArtist(artistId);
//         return Ok(new { success = true, data = artworks });
//     }

//     // ── GET BY ARTIST ID (kept for backward compat) ───────────
//     [Authorize]
//     [HttpGet("GetByArtist/{id}")]
//     public async Task<IActionResult> GetByArtist(int id)
//     {
//         // Validate caller owns this data
//         var callerArtistId = GetArtistIdFromToken();
//         if (callerArtistId != id)
//             return Forbid(); // 403 — prevents artists accessing each other's lists

//         return Ok(await _artworkRepo.GetArtworksByArtist(id));
//     }

//     // ── GET CATEGORIES ─────────────────────────────────────────
//     [HttpGet("GetCategories")]
//     public async Task<IActionResult> GetCategories()
//     {
//         var data = await _artworkRepo.GetCategories();
//         return Ok(data);
//     }

//     // ── GET ARTWORK BY ID ──────────────────────────────────────
//     [HttpGet("GetById/{id}")]
//     public async Task<IActionResult> GetById(int id)
//     {
//         var artwork = await _artworkRepo.GetById(id);
//         return artwork == null
//             ? NotFound(new { message = $"Artwork {id} not found." })
//             : Ok(artwork);
//     }

//     // ── GET APPROVED ARTWORKS ──────────────────────────────────
//     [HttpGet("GetApproved")]
//     public async Task<IActionResult> GetApproved()
//         => Ok(await _artworkRepo.GetApprovedArtworks());

//     // ── DELETE ARTWORK ─────────────────────────────────────────
//     [Authorize]
//     [HttpDelete("Delete/{id}")]
//     public async Task<IActionResult> Delete(int id)
//     {
//         var rows = await _artworkRepo.DeleteArtwork(id);
//         return rows > 0
//             ? Ok(new { success = true, message = "Artwork deleted." })
//             : NotFound(new { success = false, message = "Artwork not found." });
//     }

//     // ── UPDATE ARTWORK ─────────────────────────────────────────
//     [Authorize]
//     [HttpPut("Update")]
//     public async Task<IActionResult> Update([FromForm] t_Artwork art)
//     {
//         try
//         {
//             if (art.ArtworkFile != null && art.ArtworkFile.Length > 0)
//             {
//                 using var stream = art.ArtworkFile.OpenReadStream();
//                 var result = await _cloudinary.UploadAsync(new ImageUploadParams
//                 {
//                     File = new FileDescription(art.ArtworkFile.FileName, stream),
//                     Folder = "Artify_Gallery",
//                     UploadPreset = _config["CloudinarySettings:UploadPreset"] ?? "Cloudinary_Setup"
//                 });
//                 art.c_original_path = result.SecureUrl.ToString();
//                 art.c_preview_path = _cloudinary.Api.UrlImgUp
//                     .Transform(new Transformation()
//                         .Width(800).Crop("scale").Quality("auto")
//                         .Overlay(new TextLayer().Text("Artify").FontFamily("Arial").FontSize(40).FontWeight("bold"))
//                         .Opacity(30).Chain())
//                     .BuildUrl(result.PublicId);
//             }
//             else
//             {
//                 art.c_original_path = null;
//                 art.c_preview_path = null;
//             }

//             var rows = await _artworkRepo.UpdateArtwork(art);
//             return rows > 0
//                 ? Ok(new { success = true, message = "Artwork updated successfully." })
//                 : NotFound(new { success = false, message = "Artwork not found." });
//         }
//         catch (Exception ex)
//         {
//             return StatusCode(500, new { success = false, message = ex.Message });
//         }
//     }

//     // ── DASHBOARD — JWT claim extracts artistId ────────────────
//     [Authorize]
//     [HttpGet("dashboard")]
//     public async Task<IActionResult> GetDashboard()
//     {
//         var artistId = GetArtistIdFromToken();
//         if (artistId == 0) return Unauthorized(new { message = "Invalid token." });

//         var data = await _repo.GetDashboardData(artistId);
//         return data == null ? NotFound() : Ok(data);
//     }

//     // ── CHANGE PASSWORD ────────────────────────────────────────
//     [Authorize]
//     [HttpPost("change-password")]
//     public async Task<IActionResult> ChangePassword([FromBody] t_ChangePassword model)
//     {
//         // Extra safety: ensure the token owner matches the request body artistId
//         var callerArtistId = GetArtistIdFromToken();
//         if (callerArtistId != model.c_Artist_Id)
//             return Forbid();

//         var result = await _repo.ChangePassword(
//             model.c_Artist_Id, model.c_CurrentPassword, model.c_NewPassword);

//         if (result == 1) return Ok(new { success = true, message = "Password updated." });
//         if (result == 0) return Ok(new { success = false, message = "Incorrect current password." });
//         return BadRequest(new { success = false, message = "Update failed." });
//     }

//     // ── GET PROFILE ────────────────────────────────────────────
//     [Authorize]
//     [HttpGet("profile/{id}")]
//     public async Task<IActionResult> GetProfile(int id)
//     {
//         // Validate caller owns the profile
//         var callerArtistId = GetArtistIdFromToken();
//         if (callerArtistId != id) return Forbid();

//         var data = await _repo.GetArtistById(id);
//         return Ok(data);
//     }

//     // ── EDIT PROFILE ───────────────────────────────────────────
//     [Authorize]
//     [HttpPost("profile")]
//     public async Task<IActionResult> EditProfile([FromForm] t_ArtistProfile model)
//     {
//         var callerArtistId = GetArtistIdFromToken();
//         if (callerArtistId != model.ArtistId) return Forbid();

//         if (model.CoverImageFile != null)
//         {
//             var fileName = Guid.NewGuid() + Path.GetExtension(model.CoverImageFile.FileName);
//             var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "Cover_Images");
//             Directory.CreateDirectory(folderPath);
//             using var stream = new FileStream(Path.Combine(folderPath, fileName), FileMode.Create);
//             await model.CoverImageFile.CopyToAsync(stream);
//             model.CoverImage = fileName;
//         }

//         var result = await _repo.EditArtistProfile(model);
//         return Ok(new { success = result == 1, rows = result });
//     }

//     // ── REVENUE ────────────────────────────────────────────────
//     [Authorize]
//     [HttpGet("revenue")]
//     public async Task<IActionResult> GetRevenue()
//     {
//         var artistId = GetArtistIdFromToken();
//         if (artistId == 0) return Unauthorized();
//         return Ok(await _repo.GetMonthlyRevenue(artistId));
//     }

//     // ── CATEGORY SALES ─────────────────────────────────────────
//     [Authorize]
//     [HttpGet("category-sales")]
//     public async Task<IActionResult> GetCategorySales()
//     {
//         var artistId = GetArtistIdFromToken();
//         if (artistId == 0) return Unauthorized();
//         return Ok(await _repo.GetSalesByCategory(artistId));
//     }

//     // ── EARNINGS SUMMARY ───────────────────────────────────────
//     [Authorize]
//     [HttpGet("earnings-summary")]
//     public async Task<IActionResult> GetEarningsSummary()
//     {
//         var artistId = GetArtistIdFromToken();
//         if (artistId == 0) return Unauthorized();
//         return Ok(await _repo.GetEarningsSummary(artistId));
//     }

//     // ── LOGOUT — clears both cookie and session ────────────────
//     [HttpPost("Logout")]
//     public IActionResult Logout()
//     {
//         Response.Cookies.Delete("ArtistToken");
//         HttpContext.Session.Clear();
//         return Ok(new { success = true, message = "Logged out successfully." });
//     }

//     // ── PRIVATE HELPERS ───────────────────────────────────────

//     /// <summary>Extracts ArtistId from the validated JWT "UserId" claim.</summary>
//     private int GetArtistIdFromToken()
//     {
//         var claim = User.FindFirst("UserId");
//         return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
//     }

//     /// <summary>Generates a signed JWT valid for 7 days.</summary>
//     private string GenerateJwtToken(t_Artist user)
//     {
//         var jwtKey = _config["Jwt:Key"]!;
//         if (jwtKey.Length < 32)
//             throw new InvalidOperationException("JWT Key must be at least 32 characters.");

//         var claims = new[]
//         {
//             new Claim(JwtRegisteredClaimNames.Sub, user.c_Email),
//             new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
//             new Claim("UserId",   user.c_User_Id.ToString()),
//             new Claim("UserName", user.c_UserName),
//             new Claim("Email",    user.c_Email)
//         };

//         var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
//         var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

//         var token = new JwtSecurityToken(
//             issuer: _config["Jwt:Issuer"],
//             audience: _config["Jwt:Audience"],
//             claims: claims,
//             expires: DateTime.UtcNow.AddDays(7),
//             signingCredentials: creds);

//         return new JwtSecurityTokenHandler().WriteToken(token);
//     }
// }


// ============================================================
//  API/Controllers/ArtistApiController.cs
//
//  KEY FIX vs previous version:
//    Cookie SameSite is set to None (required for cross-origin
//    cookie delivery when API and MVC run on different ports).
//    SameSite=Lax only works when same site; cross-origin
//    (localhost:5183 → localhost:5092) requires SameSite=None
//    + Secure=true (HTTPS) or, in development, the Secure flag
//    can be omitted if using HTTP.
// ============================================================

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

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArtistApiController : ControllerBase
{
    private readonly IArtistInterface  _repo;
    private readonly IArtworkInterface _artworkRepo;
    private readonly IConfiguration    _config;
    private readonly Cloudinary        _cloudinary;

    public ArtistApiController(
        IArtistInterface  repo,
        IArtworkInterface artworkRepo,
        IConfiguration    config)
    {
        _repo        = repo;
        _artworkRepo = artworkRepo;
        _config      = config;

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
                File           = new FileDescription(user.ProfilePicture.FileName, stream),
                Folder         = "Artify_Profiles",
                Transformation = new Transformation().Width(500).Height(500).Crop("fill")
            });
            user.c_Profile_Image = up.SecureUrl?.ToString();
        }

        var rows = await _repo.Register(user);
        return rows > 0
            ? Ok(new  { success = true,  message = "Registered! Awaiting admin approval." })
            : BadRequest(new { success = false, message = "Email already registered." });
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

        if (!user.c_Is_Active)
            return Ok(new
            {
                success  = false,
                inactive = true,
                message  = "Your account is pending admin approval."
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
            HttpOnly    = true,
            SameSite    = SameSiteMode.None,   // ← cross-origin cookie delivery
            Secure      = Request.IsHttps,     // true in prod HTTPS; false on HTTP dev
            Expires     = DateTimeOffset.UtcNow.AddDays(7),
            Path        = "/"
        };

        // Development fallback: if not HTTPS, set Secure=false explicitly
        // (Remove this block when deploying to HTTPS production)
        if (!Request.IsHttps)
        {
            cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,   // Lax works fine for same-site HTTP dev
                Secure   = false,
                Expires  = DateTimeOffset.UtcNow.AddDays(7),
                Path     = "/"
            };
        }

        Response.Cookies.Append("ArtistToken", token, cookieOptions);

        // ── Store identity in session (MVC controller guard) ──
        // HttpContext.Session.SetInt32("ArtistId",     user.c_User_Id);
        // HttpContext.Session.SetString("ArtistEmail",  user.c_Email);
        // HttpContext.Session.SetString("ArtistName",   user.c_Full_Name ?? user.c_UserName);

        return Ok(new
        {
            success  = true,
            message  = "Login successful.",
            token    = token,
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

                art.c_preview_path = cloudinary.Api.UrlImgUp.Transform(new Transformation()
                    .Width(800).Crop("scale").Quality("auto")
                    .Overlay(new TextLayer().Text("Artify").FontFamily("Arial").FontSize(60).FontWeight("bold"))
                    .Opacity(30).Chain())
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
            ? Ok(new    { success = true,  message = "Artwork deleted." })
            : NotFound(new { success = false, message = "Artwork not found." });
    }

    // ── UPDATE ────────────────────────────────────────────────
    [Authorize]
    [HttpPut("Update")]
    public async Task<IActionResult> Update([FromForm] t_Artwork art)
    {
        try
        {
            if (art.ArtworkFile != null && art.ArtworkFile.Length > 0)
            {
                using var stream = art.ArtworkFile.OpenReadStream();
                var result       = await _cloudinary.UploadAsync(new ImageUploadParams
                {
                    File         = new FileDescription(art.ArtworkFile.FileName, stream),
                    Folder       = "Artify_Gallery",
                    UploadPreset = _config["CloudinarySettings:UploadPreset"] ?? "Cloudinary_Setup"
                });
                art.c_original_path = result.SecureUrl.ToString();
                art.c_preview_path  = _cloudinary.Api.UrlImgUp
                    .Transform(new Transformation()
                        .Width(800).Crop("scale").Quality("auto")
                        .Overlay(new TextLayer()
                            .Text("Artify").FontFamily("Arial").FontSize(40).FontWeight("bold"))
                        .Opacity(30).Chain())
                    .BuildUrl(result.PublicId);
            }
            else
            {
                art.c_original_path = null;
                art.c_preview_path  = null;
            }

            var rows = await _artworkRepo.UpdateArtwork(art);
            return rows > 0
                ? Ok(new    { success = true,  message = "Artwork updated." })
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
        if (result == 1) return Ok(new { success = true,  message = "Password updated." });
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

        if (model.CoverImageFile != null)
        {
            var fileName   = Guid.NewGuid() + Path.GetExtension(model.CoverImageFile.FileName);
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(),
                "..", "MVC", "wwwroot", "Cover_Images");
            Directory.CreateDirectory(folderPath);
            await using var stream = new FileStream(
                Path.Combine(folderPath, fileName), FileMode.Create);
            await model.CoverImageFile.CopyToAsync(stream);
            model.CoverImage = fileName;
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
        var creds      = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}