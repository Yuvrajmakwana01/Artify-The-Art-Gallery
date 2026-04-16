// API/Controllers/WishlistApiController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;
using Repository.Models;

[Authorize]
[Route("api/wishlist")]
[ApiController]
public class WishlistApiController : ControllerBase
{
    private readonly IWishlistInterface _wishlistRepo;
    private readonly ILogger<WishlistApiController> _logger;

    public WishlistApiController(IWishlistInterface wishlistRepo, ILogger<WishlistApiController> logger)
    {
        _wishlistRepo = wishlistRepo;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetWishlist()
    {
        try
        {
            int buyerId = GetBuyerId();

            var items = await _wishlistRepo.GetWishlistAsync(buyerId);

            return Ok(new
            {
                success = true,
                count = items.Count,
                data = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching wishlist");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] t_WishlistRequest request)
    {
        if (request == null || request.ArtworkId <= 0)
            return BadRequest(new { success = false, message = "Invalid artwork id" });

        try
        {
            int buyerId = GetBuyerId();

            bool alreadyExists = await _wishlistRepo.IsInWishlistAsync(buyerId, request.ArtworkId);
            if (alreadyExists)
            {
                return Ok(new { success = true, message = "Already in wishlist" });
            }

            await _wishlistRepo.AddToWishlistAsync(buyerId, request.ArtworkId);

            return Ok(new { success = true, message = "Added to wishlist" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // ── REMOVE from wishlist ──────────────────────────────────────────────
    [HttpPost("remove")]
    public async Task<IActionResult> Remove([FromBody] t_WishlistRequest request)
    {
        // ✅ VALIDATION
        if (request == null || request.ArtworkId <= 0)
            return BadRequest(new { success = false, message = "Invalid artwork id" });

        try
        {
            int buyerId = GetBuyerId();

            bool removed = await _wishlistRepo.RemoveFromWishlistAsync(buyerId, request.ArtworkId);

            return Ok(new
            {
                success = removed,
                message = removed ? "Removed from wishlist" : "Item not found"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing wishlist item"); // ✅ LOGGING
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // ── TOGGLE (add if not present, remove if present) ────────────────────
     [HttpPost("toggle")]
    public async Task<IActionResult> Toggle([FromBody] t_WishlistRequest request)
    {
        // ✅ VALIDATION
        if (request == null || request.ArtworkId <= 0)
            return BadRequest(new { success = false, message = "Invalid artwork id" });

        try
        {
            int buyerId = GetBuyerId();

            bool exists = await _wishlistRepo.IsInWishlistAsync(buyerId, request.ArtworkId);

            if (exists)
            {
                await _wishlistRepo.RemoveFromWishlistAsync(buyerId, request.ArtworkId);

                return Ok(new
                {
                    success = true,
                    action = "removed",
                    isWishlisted = false
                });
            }
            else
            {
                await _wishlistRepo.AddToWishlistAsync(buyerId, request.ArtworkId);

                return Ok(new
                {
                    success = true,
                    action = "added",
                    isWishlisted = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling wishlist"); // ✅ LOGGING
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // ── CLEAR entire wishlist ─────────────────────────────────────────────
    [HttpDelete("clear")]
    public async Task<IActionResult> Clear()
    {
        try
        {
            int buyerId = GetBuyerId();
            await _wishlistRepo.ClearWishlistAsync(buyerId);
            return Ok(new { success = true, message = "Wishlist cleared" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // ── CHECK if specific artwork is wishlisted ───────────────────────────
    [HttpGet("check/{artworkId}")]
    public async Task<IActionResult> Check(int artworkId)
    {
        try
        {
            int buyerId = GetBuyerId();
            bool isWishlisted = await _wishlistRepo.IsInWishlistAsync(buyerId, artworkId);
            return Ok(new { success = true, isWishlisted });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // ── HELPER ────────────────────────────────────────────────────────────
    /// <summary>
    /// Extract buyer ID from JWT claim or session.
    /// Replace this stub with your actual auth implementation.
    /// Example with JWT: int.Parse(User.FindFirst("sub")?.Value ?? "0")
    /// </summary>
    /// 
    // private int GetBuyerId()
    //     {
    //         var claim = User.FindFirst("user_id")?.Value 
    //                 ?? User.FindFirst("sub")?.Value;

    //         if (claim == null)
    //             throw new UnauthorizedAccessException("User not logged in");

    //         return int.Parse(claim);
    //     }

     private int GetBuyerId()
    {
        var claim = User.FindFirst("user_id")?.Value 
                ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(claim))
            throw new UnauthorizedAccessException("User not logged in");

        if (!int.TryParse(claim, out int buyerId))
            throw new UnauthorizedAccessException("Invalid user ID");

        return buyerId;
    }
}
