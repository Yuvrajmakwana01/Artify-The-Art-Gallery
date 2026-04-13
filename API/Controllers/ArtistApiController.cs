using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;

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

        // ✅ SINGLE OPTIMIZED API
        [HttpGet("dashboard/{artistId}")]
        public async Task<IActionResult> GetDashboard(int artistId)
        {
            var data = await _artistRepo.GetDashboardData(artistId);

            if (data == null)
                return NotFound();

            var result = new
            {
                artistName = data.c_ArtistName,
                email = data.c_Email,
                biography = data.c_Biography,
                coverImage = data.c_CoverImageName,
                rating = data.c_RatingAvg,

                totalArtworks = data.c_TotalArtworkCount,
                totalLikes = data.c_TotalLikeCount,
                totalSales = data.c_TotalSellCount
            };

            return Ok(result);
        }
    }
}