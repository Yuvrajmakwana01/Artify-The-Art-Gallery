using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;

namespace API.Controllers;

[Authorize]
[ApiController]
[Route("api/order")]
public class OrderApiController : ControllerBase
{
    private readonly IOrderInterface _orderRepo;
    private readonly InvoiceService _invoiceService;

    public OrderApiController(IOrderInterface orderRepo, InvoiceService invoiceService)
    {
        _orderRepo = orderRepo;
        _invoiceService = invoiceService;
    }

    [HttpGet("{orderId:int}")]
    public async Task<IActionResult> GetOrder(int orderId)
    {
        var buyerId = GetBuyerId();
        var order = await _orderRepo.GetOrderDetailAsync(orderId, buyerId);

        if (order is null)
        {
            return NotFound(new { success = false, message = "Order not found." });
        }

        return Ok(order);
    }

    [HttpGet("{orderId:int}/invoice")]
    public async Task<IActionResult> DownloadInvoice(int orderId)
    {
        var buyerId = GetBuyerId();
        var order = await _orderRepo.GetOrderDetailAsync(orderId, buyerId);

        if (order is null)
        {
            return NotFound(new { success = false, message = "Order not found." });
        }

        var pdf = _invoiceService.GenerateInvoice(order);
        return File(pdf, "application/pdf", $"Artify_{orderId}.pdf");
    }

    // private int GetBuyerId()
    // {
    //     var claimValue = User.FindFirst("sub")?.Value ?? User.FindFirst("user_id")?.Value;
    //     return int.TryParse(claimValue, out var buyerId) ? buyerId : 1;
    // }
     
     private int GetBuyerId()
        {
            var claim = User.FindFirst("user_id")?.Value;

            if (string.IsNullOrEmpty(claim))
                throw new UnauthorizedAccessException("User not authenticated");

            if (!int.TryParse(claim, out int buyerId))
                throw new UnauthorizedAccessException("Invalid user ID");

            return buyerId;
        }
}
