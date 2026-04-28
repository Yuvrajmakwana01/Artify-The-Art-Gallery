using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;
using Repository.Models;
using Repository.Services;
using StackExchange.Redis;

namespace API.Controllers;

[Authorize]
[Route("api/payment")]
[ApiController]
public class PaymentApiController : ControllerBase
{
    private readonly PaypalService _paypalService;
    private readonly IPaymentInterface _paymentRepo;
    private readonly IOrderInterface _orderRepo;
    private readonly InvoiceService _invoiceService;
    private readonly EmailServices _emailService;
    private readonly ILogger<PaymentApiController> _logger;
    private readonly IWebHostEnvironment _env;


    public PaymentApiController(
        PaypalService paypalService,
        IPaymentInterface paymentRepo,
        IOrderInterface orderRepo,
        InvoiceService invoiceService,
        EmailServices emailService,
        ILogger<PaymentApiController> logger)
    {
        _paypalService = paypalService;
        _paymentRepo = paymentRepo;
        _orderRepo = orderRepo;
        _invoiceService = invoiceService;
        _emailService = emailService;
        // _env = env;
        _logger = logger;
    }

    [HttpPost("create-paypal-order")]
    [HttpPost("create-order")]
    public async Task<IActionResult> CreateOrder([FromBody] List<t_CartItem> cart)
    {
        try
        {
            if (cart == null || cart.Count == 0)
                return BadRequest(new { success = false, message = "Cart cannot be empty." });

            decimal total = cart.Sum(x => x.Price);
            string paypalOrderId = await _paypalService.CreateOrderAsync(total);

            return Ok(new { success = true, orderId = paypalOrderId, amount = total });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayPal order creation failed");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("verify-paypal")]
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] t_PaymentVerify model)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(model.PaypalOrderId))
                return BadRequest(new { success = false, message = "PayPal order id is required." });

            bool isCaptured = await _paypalService.CaptureOrderAsync(model.PaypalOrderId);
            Console.WriteLine(isCaptured);

            if (!isCaptured)
                return BadRequest(new { success = false, message = "Payment capture failed." });

            int buyerId = GetBuyerIdFromContext();
            int orderId = await _paymentRepo.ProcessFullPaymentAsync(buyerId, model);
            Console.WriteLine(orderId);
            if (orderId > 0)
            {


                // ✅ DIRECT EMAIL FLOW (NO Task.Run)
                try
                {
                    var orderDetail = await _orderRepo.GetOrderDetailAsync(orderId, buyerId);
                    Console.WriteLine("Buyer Email: " + orderDetail.BuyerEmail);

                    if (orderDetail != null)
                    {
                        _logger.LogInformation("Preparing email for Order {OrderId}", orderId);

                        // Generate PDF
                        byte[] pdf = _invoiceService.GenerateInvoice(orderDetail);

                        // Load template
                        string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "PaymentSuccess.html");
                        string body = await System.IO.File.ReadAllTextAsync(templatePath);

                        // Replace placeholders
                        body = body.Replace("{UserName}", orderDetail.BuyerName)
                                   .Replace("{OrderId}", orderDetail.OrderId.ToString())
                                   .Replace("{ArtworkName}", orderDetail.Items.FirstOrDefault()?.Title ?? "Artwork")
                                   .Replace("{ArtistName}", orderDetail.Items.FirstOrDefault()?.ArtistName ?? "Artist")
                                   .Replace("{Amount}", orderDetail.TotalAmount.ToString("N2"));

                        // ✅ Correct logo path
                        string logoPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "mvc", "wwwroot", "images", "Logo.jpeg"));

                        _logger.LogInformation("Sending email to {Email}", orderDetail.BuyerEmail);

                        // Send email
                        await _emailService.SendEmailAsync(
                            orderDetail.BuyerEmail,
                            "Order Confirmation - Artify Gallery",
                            body,
                            logoPath,
                            pdf,
                            $"Invoice_{orderId}.pdf"
                        );

                        _logger.LogInformation("Email sent successfully for Order {OrderId}", orderId);
                    }
                    else
                    {
                        _logger.LogWarning("Order details not found for Order {OrderId}", orderId);
                    }
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Email sending failed for Order {OrderId}", orderId);
                }
            }


            return Ok(new { success = true, message = "Payment successful", orderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayPal verification failed");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // private int GetBuyerIdFromContext()
    // {
    //     var claimValue = User.FindFirst("sub")?.Value ?? User.FindFirst("user_id")?.Value;
    //     return int.TryParse(claimValue, out var buyerId) ? buyerId : 1;
    // }

    private int GetBuyerIdFromContext()
    {
        var claimValue = User.FindFirst("user_id")?.Value 
                    ?? User.FindFirst("sub")?.Value;

        if (claimValue == null)
            throw new UnauthorizedAccessException("User not authenticated");

        if (!int.TryParse(claimValue, out int buyerId))
            throw new UnauthorizedAccessException("Invalid user ID");

        return buyerId;
    }

    private Task PublishArtistNotificationAsync(int orderId, List<t_CartItem> cart)
    {
        _logger.LogInformation("Artist notification placeholder executed for order {OrderId} with {Count} items", orderId, cart.Count);
        return Task.CompletedTask;
    }
}
