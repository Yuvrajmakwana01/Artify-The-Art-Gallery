using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Repository.Interfaces;
using Repository.Models;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminArtistController : ControllerBase
    {
        private readonly IAdminArtistInterface _artistRepository;

        public AdminArtistController(IAdminArtistInterface artistRepository)
        {
            _artistRepository = artistRepository;
        }


        [HttpGet("artists")]
        public async Task<IActionResult> GetArtists([FromQuery] string? search, [FromQuery] string? status)
        {
            var data = await _artistRepository.GetArtistsAsync(search, status);
            return Ok(data);
        }

        [HttpGet("artists/stats")]
        public async Task<IActionResult> GetArtistStats()
        {
            var data = await _artistRepository.GetArtistStatsAsync();
            return Ok(data);
        }

        [HttpGet("artists/{id:int}")]
        public async Task<IActionResult> GetArtistById(int id)
        {
            var data = await _artistRepository.GetArtistByIdAsync(id);
            return data is null ? NotFound() : Ok(data);
        }

        [HttpPost("artists")]
        public async Task<IActionResult> AddArtist([FromBody] AdminArtistUpsertRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ArtistName) || string.IsNullOrWhiteSpace(request.ArtistEmail))
                return BadRequest("ArtistName and ArtistEmail are required.");

            var id = await _artistRepository.AddArtistAsync(request);
            var item = await _artistRepository.GetArtistByIdAsync(id);
            return CreatedAtAction(nameof(GetArtistById), new { id }, item);
        }

        [HttpPut("artists/{id:int}")]
        public async Task<IActionResult> UpdateArtist(int id, [FromBody] AdminArtistUpsertRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ArtistName) || string.IsNullOrWhiteSpace(request.ArtistEmail))
                return BadRequest("ArtistName and ArtistEmail are required.");

            var ok = await _artistRepository.UpdateArtistAsync(id, request);
            return ok ? Ok(new { message = "Artist updated successfully." }) : NotFound();
        }

        [HttpDelete("artists/{id:int}")]
        public async Task<IActionResult> DeleteArtist(int id)
        {
            try
            {
                var ok = await _artistRepository.DeleteArtistAsync(id);
                return ok ? Ok(new { message = "Artist deleted successfully." }) : NotFound();
            }
            catch (PostgresException ex) when (ex.SqlState == "23503")
            {
                return Conflict("Cannot delete this artist because related records exist.");
            }
        }
    }
}