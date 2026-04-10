// using System;
// using System.Collections.Generic;
// using System.IdentityModel.Tokens.Jwt;
// using System.Linq;
// using System.Security.Claims;
// using System.Text;
// using System.Threading.Tasks;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.IdentityModel.Tokens;
// using Repository.Interfaces;
// using Repository.Models;
// using CloudinaryDotNet; 
// using CloudinaryDotNet.Actions; 

// namespace API.Controllers
// {
//     [ApiController]
//     [Route("api/[controller]")]
//     public class ArtistApiController : ControllerBase
//     {

//         private readonly IArtistInterface _artistRepo;

//         private readonly IArtworkInterface _artworkRepo;


//         private readonly IConfiguration myConfig;

//         public ArtistApiController(IArtistInterface artistRepo, IArtworkInterface artworkRepo, IConfiguration config)
//         {
//             _artistRepo = artistRepo;
//             myConfig = config;
//             _artworkRepo = artworkRepo;
//         }

//         [HttpPost("Register")]
//         public async Task<IActionResult> Register([FromForm] t_Artist user)
//         {
//             if (user.ProfilePicture != null)
//             {
//                 var fileName = Guid.NewGuid() + Path.GetExtension(user.ProfilePicture.FileName);

//                 // 1. Correct the folder name to match your actual project (MainMvc)
//                 var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "Artist_Images");

//                 // 2. Check if the directory exists; if not, create it
//                 if (!Directory.Exists(folderPath))
//                 {
//                     Directory.CreateDirectory(folderPath);
//                 }

//                 var filePath = Path.Combine(folderPath, fileName);

//                 // 3. Save the file
//                 using (var stream = new FileStream(filePath, FileMode.Create))
//                 {
//                     await user.ProfilePicture.CopyToAsync(stream);
//                 }

//                 user.c_Profile_Image = fileName;
//             }

//             if (user.CoverPicture != null)
//             {
//                 var fileName = Guid.NewGuid() + Path.GetExtension(user.CoverPicture.FileName);

//                 var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "Cover_Images");

//                 if (!Directory.Exists(folderPath))
//                 {
//                     Directory.CreateDirectory(folderPath);
//                 }

//                 var filePath = Path.Combine(folderPath, fileName);

//                 using (var stream = new FileStream(filePath, FileMode.Create))
//                 {
//                     await user.CoverPicture.CopyToAsync(stream);
//                 }

//                 user.c_Cover_Image = fileName;
//             }

//             var status = await _artistRepo.Register(user);
//             return status == 1 ? Ok(new { success = true, message = "Artist Registered" }) :
//                                  Ok(new { success = false, message = "Artist already exists" });
//         }


//         [HttpPost("Login")]
//         public async Task<IActionResult> Login([FromForm] vm_Login user)
//         {
//             t_Artist UserData = await _artistRepo.Login(user);

//             // FIX: Added null check to prevent NullReferenceException
//             if (UserData != null && UserData.c_User_Id != 0)
//             {
//                 // Note: Ensure Session is configured in Program.cs to use this
//                 HttpContext.Session.SetString("UserName", UserData.c_UserName);

//                 var claims = new[]
//                 {
//                     new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
//                     new Claim("UserId", UserData.c_User_Id.ToString()),
//                     new Claim("UserName", UserData.c_UserName)
//                 };

//                 var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(myConfig["Jwt:Key"]));
//                 var signIn = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

//                 var token = new JwtSecurityToken(
//                     issuer: myConfig["Jwt:Issuer"],
//                     audience: myConfig["Jwt:Audience"],
//                     claims: claims,
//                     expires: DateTime.UtcNow.AddDays(1),
//                     signingCredentials: signIn
//                 );

//                 return Ok(new
//                 {
//                     success = true,
//                     message = "User logged in successfully.",
//                     UserData = UserData,
//                     token = new JwtSecurityTokenHandler().WriteToken(token)
//                 });
//             }

//             return Ok(new { success = false, message = "Invalid credentials." });
//         }

//         [HttpPost("Upload")]
//         public async Task<IActionResult> Upload([FromForm] t_Artwork art)
//         {
//             if (art.ArtworkFile == null || art.ArtworkFile.Length == 0)
//             {
//                 return BadRequest("Please select an image to upload.");
//             }

//             var account = new Account(
//                 myConfig["CloudinarySettings:CloudName"],
//                 myConfig["CloudinarySettings:ApiKey"],
//                 myConfig["CloudinarySettings:ApiSecret"]
//             );

//             var cloudinary = new Cloudinary(account);

//             try
//             {
//                 using (var stream = art.ArtworkFile.OpenReadStream())
//                 {
//                     var uploadParams = new ImageUploadParams()
//                     {
//                         File = new FileDescription(art.ArtworkFile.FileName, stream),
//                         Folder = "Artify_Gallery",
//                         UploadPreset = myConfig["CloudinarySettings:UploadPreset"] ?? "Cloudinary_Setup"
//                     };

//                     var uploadResult = await cloudinary.UploadAsync(uploadParams);

//                     if (uploadResult.Error != null)
//                         return BadRequest(uploadResult.Error.Message);

//                     art.c_original_path = uploadResult.SecureUrl.ToString();

//                     art.c_preview_path = cloudinary.Api.UrlImgUp.Transform(new Transformation()
//                         .Width(800).Crop("scale").Quality("auto")
//                         .Overlay(new TextLayer().Text("Artify Preview").FontFamily("Arial").FontSize(60).FontWeight("bold"))
//                         .Opacity(30).Chain())
//                         .BuildUrl(uploadResult.PublicId);
//                 }

//                 var status = await _artworkRepo.UploadArtwork(art);
//                 return status > 0 ? Ok(new { success = true, message = "Masterpiece uploaded!" })
//                                   : BadRequest("Failed to save artwork to database.");
//             }
//             catch (Exception ex)
//             {
//                 return StatusCode(500, $"Internal server error: {ex.Message}");
//             }
//         }

//         [HttpGet("GetAll")]
//         public async Task<IActionResult> GetAll()
//         {
//             try
//             {
//                 var artworks = await _artworkRepo.GetAllArtworks();
//                 return Ok(new { success = true, data = artworks });
//             }
//             catch (Exception ex)
//             {
//                 return StatusCode(500, $"Error: {ex.Message}");
//             }
//         }


//         [HttpGet("GetCategories")]
//         public async Task<IActionResult> GetCategories()
//         {
//             try
//             {
//                 var data = await _artworkRepo.GetCategories();
//                 return Ok(data);
//             }
//             catch (Exception ex)
//             {
//                 return BadRequest(ex.Message);
//             }
//         }


//         public async Task<IActionResult> Logout()
//         {
//             // Clear session data
//             HttpContext.Session.Clear();

//             // If using cookie authentication, sign out
//             // await HttpContext.SignOutAsync();

//             return Ok(new { success = true, message = "Logged out successfully." });
//         }
//     }
// }


using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Repository.Interfaces;
using Repository.Models;
using CloudinaryDotNet; 
using CloudinaryDotNet.Actions; 

namespace API.Controllers
{
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
                        .Overlay(new TextLayer().Text("Artify Preview").FontFamily("Arial").FontSize(60).FontWeight("bold"))
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

        [HttpPost("Logout")]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            return Ok(new { success = true, message = "Logged out successfully." });
        }
    }
}