using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;
using Repository.Models;

namespace MyApp.Namespace
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserProfileController : ControllerBase
    {
        private readonly IUserProfileInterface _profileRepo;

        public UserProfileController(IUserProfileInterface profileRepo)
        {
            _profileRepo = profileRepo;
        }

        // GET api/UserProfileApi/GetProfile/5
        [HttpGet("GetProfile/{userId}")]
        public async Task<IActionResult> GetProfile(int userId)
        {
            var profile = await _profileRepo.GetProfileById(userId);
            if (profile == null)
                return NotFound(new { success = false, message = "User not found" });

            return Ok(new { success = true, data = profile });
        }

        // ✅ Fix - validation add karo aur debug karo
        [HttpPut("UpdateProfile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UserProfile profile)
        {
            Console.WriteLine("UpdateProfile called - UserId: " + profile.c_UserId);
            Console.WriteLine("FullName: " + profile.c_FullName);
            Console.WriteLine("Email: " + profile.c_Email);

            if (profile.c_UserId == 0)
                return BadRequest(new { success = false, message = "Invalid user id" });

            var result = await _profileRepo.UpdateProfile(profile);

            Console.WriteLine("Update result: " + result);

            if (result > 0)
                return Ok(new { success = true, message = "Profile updated successfully" });

            return BadRequest(new { success = false, message = "Failed to update profile" });
        }
    }
}
