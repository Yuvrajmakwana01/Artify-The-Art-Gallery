using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;

namespace API.Controllers;


[ApiController]
[Route("api/artwork")]
public class BuyerUiArtworkApiController : ControllerBase
{
    private readonly IBuyerUiArtworkInterface _artworkRepo;

    public BuyerUiArtworkApiController(IBuyerUiArtworkInterface artworkRepo)
    {
        _artworkRepo = artworkRepo;
    }

    // GET api/artwork
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var artworks = await _artworkRepo.GetAllApprovedAsync();

            return Ok(new
            {
                success = true,
                count = artworks.Count,
                data = artworks
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = ex.Message
            });
        }
    }

    // GET api/artwork/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Invalid artwork id" });

        try
        {
            var artwork = await _artworkRepo.GetByIdAsync(id);

            if (artwork is null)
                return NotFound(new { success = false, message = "Artwork not found." });

            return Ok(new
            {
                success = true,
                data = artwork
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = ex.Message
            });
        }
    }
}
