using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Repository.Interfaces;
using Repository.Models;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]

    public class AdminUserController : ControllerBase
    {
        private readonly IAdminUsersInterface _userRepository;

        public AdminUserController(IAdminUsersInterface userRepository)
        {
            _userRepository = userRepository;
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
    }
}