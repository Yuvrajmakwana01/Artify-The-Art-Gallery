using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;
using Repository.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArtistApiController : ControllerBase
    {
        private readonly IArtistInterface _artistRepo;

        public ArtistApiController(IArtistInterface artistRepo)
        {
            _artistRepo = artistRepo;
        }

        [HttpGet("GetEditProfile")]
        public async Task<IActionResult> GetEditProfile()
        {
            int artistId = 1;

            var data = await _artistRepo.GetArtistById(artistId);

            if (data == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Artist not found"
                });
            }

            return Ok(new
            {
                success = true,
                data
            });
        }

        [HttpPost("EditProfile")]
        public async Task<IActionResult> EditProfile([FromForm] t_ArtistProfile model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid model",
                    errors = ModelState
                });
            }

            try
            {
                Console.WriteLine("----- EDIT PROFILE API CALLED -----");

                model.ArtistId = 1;

                // SAFE URL VALIDATION
                if (model.Urls != null && model.Urls.Any())
                {
                    foreach (var url in model.Urls)
                    {
                        if (!string.IsNullOrWhiteSpace(url) &&
                            !Uri.TryCreate(url, UriKind.Absolute, out _))
                        {
                            return BadRequest(new
                            {
                                success = false,
                                message = $"Invalid URL: {url}"
                            });
                        }
                    }
                }

                // SAFE IMAGE UPLOAD
                if (model.CoverImageFile != null && model.CoverImageFile.Length > 0)
                {
                    var ext = Path.GetExtension(model.CoverImageFile.FileName)?.ToLower();
                    var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };

                    if (ext == null || !allowed.Contains(ext))
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "Only jpg, jpeg, png, webp allowed"
                        });
                    }

                    var fileName = $"cover_{Guid.NewGuid()}{ext}";

                    var folderPath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "..", "MainMvc", "wwwroot", "Cover_Images"
                    );

                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

                    var fullPath = Path.Combine(folderPath, fileName);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await model.CoverImageFile.CopyToAsync(stream);
                    }

                    model.CoverImage = fileName;
                }

                var result = await _artistRepo.EditArtistProfile(model);

                return result switch
                {
                    1 => Ok(new { success = true, message = "Profile updated successfully" }),
                    0 => NotFound(new { success = false, message = "Artist not found" }),
                    _ => StatusCode(500, new { success = false, message = "Unknown error" })
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.ToString());

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message // 🔥 SHOW REAL ERROR
                });
            }
        }
    }
}