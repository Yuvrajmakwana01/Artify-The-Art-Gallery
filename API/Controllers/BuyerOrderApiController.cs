using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;

namespace MyApp.Namespace
{
    [Route("api/BuyerOrder")]
    [ApiController]
    public class BuyerOrderApiController : ControllerBase
    {
        private readonly IBuyerOrderInterface _orderRepo;

        public BuyerOrderApiController(IBuyerOrderInterface orderRepo)
        {
            _orderRepo = orderRepo;
        }

        // // Matches: GET http://localhost:5183/api/BuyerOrder/GetOrders/1
        // [HttpGet("GetOrders/{buyerId:int}")]
        // public async Task<IActionResult> GetOrders(int buyerId)
        // {
        //     var list = await _orderRepo.GetOrderSummariesAsync(buyerId);
        //     return Ok(list);
        // }

        // // Matches: GET http://localhost:5183/api/BuyerOrder/GetOrderDetail/42
        // [HttpGet("GetOrderDetail/{orderId:int}")]
        // public async Task<IActionResult> GetOrderDetail(int orderId)
        // {
        //     // buyerId hardcoded until login is added
        //     int buyerId = 1;
        //     var detail = await _orderRepo.GetOrderDetailAsync(orderId, buyerId);
        //     if (detail is null)
        //         return NotFound(new { success = false, message = "Order not found." });

        //     return Ok(detail);
        // }


        [Authorize]
        [HttpGet("GetOrders")]
        public async Task<IActionResult> GetOrders()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var list = await _orderRepo.GetOrderSummariesAsync(userId);
            return Ok(list);
        }

        [Authorize]
        [HttpGet("GetOrderDetail/{orderId:int}")]
        public async Task<IActionResult> GetOrderDetail(int orderId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var detail = await _orderRepo.GetOrderDetailAsync(orderId, userId);

            if (detail == null)
                return NotFound();

            return Ok(detail);
        }
    }
}