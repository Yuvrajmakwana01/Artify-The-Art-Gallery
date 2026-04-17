// using Microsoft.AspNetCore.Http;
// using Microsoft.AspNetCore.Mvc;
// using Repository.Interfaces;
// using Repository.Models;

// namespace MyApp.Namespace
// {
//     [Route("api/[controller]")]
//     [ApiController]
//     public class UserProfileController : ControllerBase
//     {
//         private readonly IUserProfileInterface _profileRepo;

//         public UserProfileController(IUserProfileInterface profileRepo)
//         {
//             _profileRepo = profileRepo;
//         }

//         [HttpGet("GetProfile/{userId}")]
//         public async Task<IActionResult> GetProfile(int userId)
//         {
//             var profile = await _profileRepo.GetProfileById(userId);
//             if (profile == null)
//                 return NotFound(new { success = false, message = "User not found" });

//             return Ok(new { success = true, data = profile });
//         }

//         [HttpPut("UpdateProfile")]
//         public async Task<IActionResult> UpdateProfile([FromForm] UserProfile profile)
//         {
//             if (profile.c_UserId == 0)
//                 return BadRequest(new { success = false, message = "Invalid user id" });

//             // ✅ Handle image upload
//             if (profile.c_ImageFile != null && profile.c_ImageFile.Length > 0)
//             {
//                 string fileName = "user" + profile.c_UserId + "_" + DateTime.Now.Ticks + Path.GetExtension(profile.c_ImageFile.FileName);

//                 string uploadFolder = Path.GetFullPath(
//                     Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "profile_images")
//                 );

//                 if (!Directory.Exists(uploadFolder))
//                     Directory.CreateDirectory(uploadFolder);

//                 string filePath = Path.Combine(uploadFolder, fileName);

//                 using (var stream = new FileStream(filePath, FileMode.Create))
//                 {
//                     await profile.c_ImageFile.CopyToAsync(stream);
//                 }

//                 profile.c_Image = "/profile_images/" + fileName;
//             }
//             // else: profile.c_Image already has the old path sent from frontend — no change needed

//             var result = await _profileRepo.UpdateProfile(profile);

//             if (result > 0)
//                 return Ok(new { success = true, message = "Profile updated successfully" });

//             return BadRequest(new { success = false, message = "Failed to update profile" });
//         }

//         [HttpPost("change-password")]
//         public async Task<IActionResult> ChangePassword([FromBody] t_ChangePassword model)
//         {
//             // The [ApiController] attribute triggers automatic validation here.
//             // If validation fails, it returns 400 automatically.

//             var result = await _profileRepo.ChangePassword(
//                 model.c_user_id,
//                 model.c_CurrentPassword,
//                 model.c_NewPassword

//             );

//             if (result == 1) return Ok(new { success = true });
//             if (result == 0) return Ok(new { success = false, message = "Incorrect current password" });

//             return BadRequest(new { success = false, message = "Update failed or user not found" });
//         }
//     }
// }




using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;
using Repository.Models;

namespace MyApp.Namespace
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // 🔥 Apply globally
    public class UserProfileController : ControllerBase
    {
        private readonly IUserProfileInterface _profileRepo;

        public UserProfileController(IUserProfileInterface profileRepo)
        {
            _profileRepo = profileRepo;
        }

        // ✅ GET PROFILE (LOGGED-IN USER ONLY)
        [HttpGet("GetProfile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var profile = await _profileRepo.GetProfileById(userId);

            if (profile == null)
                return NotFound(new { success = false });

            return Ok(new { success = true, data = profile });
        }

        // ✅ UPDATE PROFILE (FORCE USER ID FROM TOKEN)
        [HttpPut("UpdateProfile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UserProfile profile)
        {
            // var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst("user_id")?.Value
            ?? User.FindFirst("c_UserId")?.Value;

            if (userIdClaim == null)
                return Unauthorized();

            var userId = int.Parse(userIdClaim);

            profile.c_UserId = userId; // 🔥 IMPORTANT FIX

            // Image upload (your same code)
            if (profile.c_ImageFile != null && profile.c_ImageFile.Length > 0)
            {
                string fileName = "user" + userId + "_" + DateTime.Now.Ticks + Path.GetExtension(profile.c_ImageFile.FileName);

                string uploadFolder = Path.GetFullPath(
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "profile_images")
                );

                if (!Directory.Exists(uploadFolder))
                    Directory.CreateDirectory(uploadFolder);

                string filePath = Path.Combine(uploadFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profile.c_ImageFile.CopyToAsync(stream);
                }

                profile.c_Image = "/profile_images/" + fileName;
            }

            var result = await _profileRepo.UpdateProfile(profile);

            if (result > 0)
                return Ok(new { success = true });

            return BadRequest(new { success = false });
        }

        // ✅ CHANGE PASSWORD (SECURE)
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] t_ChangePassword model)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var result = await _profileRepo.ChangePassword(
                userId,
                model.c_CurrentPassword,
                model.c_NewPassword
            );

            if (result == 1) return Ok(new { success = true });
            if (result == 0) return BadRequest(new { success = false, message = "Wrong password" });

            return BadRequest(new { success = false });
        }
    }
}