using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Implementations;
using Repository.Models;
using Repository.Services;

namespace API.Controllers
{
    [ApiController]
    [Route("api/admin/artworks")]
    [Authorize(Roles = "Admin")]
    public class AdminArtworkModerationApiController : ControllerBase
    {
        private readonly AdminArtworkService _service;

        public AdminArtworkModerationApiController(AdminArtworkService service)
        {
            _service = service;
        }

        // GET /api/admin/artworks?status=All&page=1&pageSize=10
        [HttpGet]
        public async Task<IActionResult> GetArtworks(
            [FromQuery] string? status = "All",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            try
            {
                var result = await _service.GetArtworksAsync(status, page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST /api/admin/artworks/{id}/approve
        [HttpPost("{id:int}/approve")]
        public async Task<IActionResult> Approve(
            int id,
            [FromBody] ApproveArtworkRequest request)
        {
            try
            {
                await _service.ApproveArtworkAsync(id, request.c_AdminNote);
                return Ok(new { success = true, message = "Artwork approved successfully." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST /api/admin/artworks/{id}/reject
        [HttpPost("{id:int}/reject")]
        public async Task<IActionResult> Reject(
            int id,
            [FromBody] RejectArtworkRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.c_AdminNote))
                return BadRequest(new { error = "Admin note is required when rejecting an artwork." });

            try
            {
                await _service.RejectArtworkAsync(id, request.c_AdminNote);
                return Ok(new { success = true, message = "Artwork rejected." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
