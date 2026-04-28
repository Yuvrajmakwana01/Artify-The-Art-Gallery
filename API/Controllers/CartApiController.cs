using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Models;

namespace API.Controllers;

[Authorize]
[ApiController]
[Route("api/cart")]
public class CartApiController : ControllerBase
{
   [HttpPost("sync")]
    public IActionResult SyncCart([FromBody] List<t_CartItem> cart)
    {
        if (cart == null || !cart.Any())
            return BadRequest(new { success = false, message = "Cart is empty" });

        int userId = GetUserId();

        // 👉 TODO: Save to DB (for now just return)
        return Ok(new
        {
            success = true,
            message = "Cart synced successfully",
            count = cart.Count
        });
    }

    [HttpPost("add")]
    public IActionResult Add([FromBody] t_CartSyncRequest request)
    {
        if (request == null || request.ArtworkId <= 0)
            return BadRequest(new { success = false, message = "Invalid artwork id" });

        int userId = GetUserId();

        // 👉 TODO: Save in DB

        return Ok(new
        {
            success = true,
            message = $"Artwork {request.ArtworkId} added to cart.",
            userId
        });
    }
    

    [HttpPost("remove")]
    public IActionResult Remove([FromBody] t_CartSyncRequest request)
    {
        if (request == null || request.ArtworkId <= 0)
            return BadRequest(new { success = false, message = "Invalid artwork id" });

        int userId = GetUserId();

        // 👉 TODO: Remove from DB

        return Ok(new
        {
            success = true,
            message = $"Artwork {request.ArtworkId} removed from cart.",
            userId
        });
    }
    

    private int GetUserId()
    {
        var claim = User.FindFirst("user_id")?.Value;

        if (string.IsNullOrEmpty(claim))
            throw new UnauthorizedAccessException("User not authenticated");

        if (!int.TryParse(claim, out int userId))
            throw new UnauthorizedAccessException("Invalid user ID");

        return userId;
    }

}
