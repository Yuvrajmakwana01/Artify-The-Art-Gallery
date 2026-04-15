using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;
using Repository.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]

public class ArtistApiController : ControllerBase
{
    private readonly IArtistInterface _artistRepo;
    private readonly IArtworkInterface _artworkRepo;
    private readonly IConfiguration myConfig;

    public ArtistApiController(IArtistInterface artistRepo, IArtworkInterface artworkRepo, IConfiguration config)
    {
        _artistRepo = artistRepo;
        myConfig = config;
        _artworkRepo = artworkRepo;
    }


    [HttpPost("Register")]
    public async Task<IActionResult> Register([FromForm] t_Artist user)
    {
        if (user.ProfilePicture != null)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(user.ProfilePicture.FileName);
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "Artist_Images");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var filePath = Path.Combine(folderPath, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await user.ProfilePicture.CopyToAsync(stream);
            }
            user.c_Profile_Image = fileName;
        }

        if (user.CoverPicture != null)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(user.CoverPicture.FileName);
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "Cover_Images");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var filePath = Path.Combine(folderPath, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await user.CoverPicture.CopyToAsync(stream);
            }
            user.c_Cover_Image = fileName;
        }

        var status = await _artistRepo.Register(user);
        return status == 1 ? Ok(new { success = true, message = "Artist Registered" }) :
                             Ok(new { success = false, message = "Artist already exists" });
    }

    [HttpPost("Login")]
    public async Task<IActionResult> Login([FromForm] vm_Login user)
    {
        t_Artist UserData = await _artistRepo.Login(user);

        if (UserData != null && UserData.c_User_Id != 0)
        {
            HttpContext.Session.SetString("UserName", UserData.c_UserName);

            var claims = new[]
            {
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim("UserId", UserData.c_User_Id.ToString()),
                    new Claim("UserName", UserData.c_UserName)
                };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(myConfig["Jwt:Key"]));
            var signIn = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: myConfig["Jwt:Issuer"],
                audience: myConfig["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: signIn
            );

            return Ok(new
            {
                success = true,
                message = "User logged in successfully.",
                UserData = UserData,
                token = new JwtSecurityTokenHandler().WriteToken(token)
            });
        }

        return Ok(new { success = false, message = "Invalid credentials." });
    }


    [HttpPost("Upload")]
    public async Task<IActionResult> Upload([FromForm] t_Artwork art)
    {
        if (art.ArtworkFile == null || art.ArtworkFile.Length == 0)
        {
            return BadRequest("Please select an image to upload.");
        }

        var account = new Account(
            myConfig["CloudinarySettings:CloudName"],
            myConfig["CloudinarySettings:ApiKey"],
            myConfig["CloudinarySettings:ApiSecret"]
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
                    UploadPreset = myConfig["CloudinarySettings:UploadPreset"] ?? "Cloudinary_Setup"
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

    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var artworks = await _artworkRepo.GetAllArtworks();
            return Ok(new { success = true, data = artworks });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    [HttpGet("GetCategories")]
    public async Task<IActionResult> GetCategories()
    {
        try
        {
            var data = await _artworkRepo.GetCategories();
            return Ok(data);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }


    [HttpGet("GetById/{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            // Fetch the artwork from the repository
            var artwork = await _artworkRepo.GetById(id);

            // Check if the artwork exists
            if (artwork == null)
            {
                return NotFound(new { message = $"Artwork with ID {id} not found." });
            }

            // Return the successfully found object
            return Ok(artwork);
        }
        catch (Exception ex)
        {
            // Log the exception (using your logger) and return 500
            return StatusCode(500, new { message = "An error occurred while retrieving the data.", details = ex.Message });
        }
    }



    [HttpGet("GetApproved")]
    public async Task<IActionResult> GetApproved() => Ok(await _artworkRepo.GetApprovedArtworks());


    [HttpGet("GetByArtist/{id}")]
    public async Task<IActionResult> GetByArtist(int id) => Ok(await _artworkRepo.GetArtworksByArtist(id));


    [HttpDelete("Delete/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var rowsAffected = await _artworkRepo.DeleteArtwork(id);

        if (rowsAffected > 0)
        {
            return Ok(new
            {
                success = true,
                message = "Artwork deleted successfully."
            });
        }
        else
        {
            return NotFound(new
            {
                success = false,
                message = "Artwork not found or could not be deleted."
            });
        }
    }



    [HttpPut("Update")]
    public async Task<IActionResult> Update([FromForm] t_Artwork art)
    {
        try
        {
            // 1. Only process Cloudinary if a new file is actually uploaded
            if (art.ArtworkFile != null && art.ArtworkFile.Length > 0)
            {
                var cloudinary = new Cloudinary(new Account(
                    myConfig["CloudinarySettings:CloudName"],
                    myConfig["CloudinarySettings:ApiKey"],
                    myConfig["CloudinarySettings:ApiSecret"]));

                using var stream = art.ArtworkFile.OpenReadStream();
                var result = await cloudinary.UploadAsync(new ImageUploadParams
                {
                    File = new FileDescription(art.ArtworkFile.FileName, stream),
                    Folder = "Artify_Gallery",
                    UploadPreset = myConfig["CloudinarySettings:UploadPreset"] ?? "Cloudinary_Setup"

                });

                // Update model with new paths
                art.c_original_path = result.SecureUrl.ToString();
                art.c_preview_path = cloudinary.Api.UrlImgUp.Transform(new Transformation()
                    .Width(800).Crop("scale").Quality("auto")
                    .Overlay(new TextLayer().Text("Artify").FontFamily("Arial").FontSize(40).FontWeight("bold"))
                    .Opacity(30).Chain())
                    .BuildUrl(result.PublicId);
            }
            else
            {
                // Explicitly set these to null so the Repository knows NOT to update them
                art.c_original_path = null;
                art.c_preview_path = null;
            }

            // 2. Call the repository
            var resultRows = await _artworkRepo.UpdateArtwork(art);

            return resultRows > 0
                ? Ok(new { success = true, message = "Artwork updated successfully" })
                : NotFound(new { success = false, message = "Artwork not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // ================= DASHBOARD =================
    // [Authorize]
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            // 🔥 Extract UserId from JWT Token
            var userIdClaim = User.FindFirst("UserId");

            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "Invalid token" });
            }

            int artistId = int.Parse(userIdClaim.Value);

            var data = await _artistRepo.GetDashboardData(artistId);

            return data == null ? NotFound() : Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }


    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] t_ChangePassword model)
    {
        // The [ApiController] attribute triggers automatic validation here.
        // If validation fails, it returns 400 automatically.

        var result = await _artistRepo.ChangePassword(
            model.c_Artist_Id,
            model.c_CurrentPassword,
            model.c_NewPassword

        );

        if (result == 1) return Ok(new { success = true });
        if (result == 0) return Ok(new { success = false, message = "Incorrect current password" });

        return BadRequest(new { success = false, message = "Update failed or user not found" });
    }



    // ================= PROFILE =================
    [HttpGet("profile/{id}")]
    public async Task<IActionResult> GetProfile(int id)
    {
        var data = await _artistRepo.GetArtistById(id);
        return Ok(data);
    }

    [HttpPost("profile")]
    public async Task<IActionResult> EditProfile([FromForm] t_ArtistProfile model)
    {
        // 1. Handle File Upload if present
        if (model.CoverImageFile != null)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(model.CoverImageFile.FileName);
            // Ensure path matches your project structure
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "Cover_Images");

            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(folderPath, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.CoverImageFile.CopyToAsync(stream);
            }
            model.CoverImage = fileName; // This is what gets saved to DB
        }

        // 2. Call Repo
        var result = await _artistRepo.EditArtistProfile(model);
        return Ok(result);
    }


    // [Authorize]
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue()
    {
        var userId = int.Parse(User.FindFirst("UserId").Value);
        var data = await _artistRepo.GetMonthlyRevenue(userId);
        return Ok(data);
    }

    // [Authorize]
    [HttpGet("category-sales")]
    public async Task<IActionResult> GetCategorySales()
    {
        var userId = int.Parse(User.FindFirst("UserId").Value);
        var data = await _artistRepo.GetSalesByCategory(userId);
        return Ok(data);
    }



    // [Authorize]
    [HttpGet("earnings-summary")]
    public async Task<IActionResult> GetEarningsSummary()
    {
        var userId = int.Parse(User.FindFirst("UserId").Value);

        var data = await _artistRepo.GetEarningsSummary(userId);

        return Ok(data);
    }

    [HttpPost("Logout")]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new { success = true, message = "Logged out successfully." });
    }
}