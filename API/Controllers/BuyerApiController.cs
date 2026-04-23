using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Services;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BuyerApiController : ControllerBase
    {
        private readonly RedisService _redis;

        public BuyerApiController(RedisService redis)
        {
            _redis = redis;
        }

        [Authorize]
        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications([FromQuery] int take = 20)
        {
            var buyerId = GetBuyerIdFromToken();
            if (buyerId == 0)
                return Unauthorized(new { success = false, message = "Invalid token." });

            if (take < 1) take = 1;
            if (take > 50) take = 50;

            var recipientId = buyerId.ToString();
            var data = await _redis.GetNotificationsAsync("buyer", recipientId, take);
            var unreadCount = await _redis.GetNotificationCountAsync("buyer", recipientId);

            return Ok(new
            {
                success = true,
                unreadCount,
                data
            });
        }

        [Authorize]
        [HttpPost("notifications/read")]
        public async Task<IActionResult> MarkNotificationsRead()
        {
            var buyerId = GetBuyerIdFromToken();
            if (buyerId == 0)
                return Unauthorized(new { success = false, message = "Invalid token." });

            await _redis.ClearNotificationsAsync("buyer", buyerId.ToString());
            return Ok(new { success = true });
        }

        [Authorize]
        [HttpPost("notifications/{notificationId}/read")]
        public async Task<IActionResult> MarkNotificationRead([FromRoute] string notificationId)
        {
            var buyerId = GetBuyerIdFromToken();
            if (buyerId == 0)
                return Unauthorized(new { success = false, message = "Invalid token." });

            if (string.IsNullOrWhiteSpace(notificationId))
                return BadRequest(new { success = false, message = "Notification id is required." });

            var recipientId = buyerId.ToString();
            var removed = await _redis.MarkAsReadAsync("buyer", recipientId, notificationId);

            if (!removed)
                return NotFound(new { success = false, message = "Notification not found." });

            var unreadCount = await _redis.GetNotificationCountAsync("buyer", recipientId);

            return Ok(new
            {
                success = true,
                unreadCount
            });
        }

        private int GetBuyerIdFromToken()
        {
            var claimValue = User.FindFirst("user_id")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            return int.TryParse(claimValue, out var buyerId) ? buyerId : 0;
        }
    }
}
