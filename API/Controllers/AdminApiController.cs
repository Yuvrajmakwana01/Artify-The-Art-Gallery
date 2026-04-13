using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Repository;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminApiController : ControllerBase
    {
        private readonly IAdminInterface _adminRepo;

        public AdminApiController(IAdminInterface adminRepo)
        {
            _adminRepo = adminRepo;
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
   
   
    }
}
