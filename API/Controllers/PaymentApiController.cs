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
    private readonly EmailService _emailService;
    private readonly RabbitService _rabbit;
    private readonly ILogger<PaymentApiController> _logger;
    private readonly IWebHostEnvironment _env;


    public PaymentApiController(
        PaypalService paypalService,
        IPaymentInterface paymentRepo,
        IOrderInterface orderRepo,
        InvoiceService invoiceService,
        EmailService emailService,
        RabbitService rabbit,
        ILogger<PaymentApiController> logger)
    {
        _paypalService = paypalService;
        _paymentRepo = paymentRepo;
        _orderRepo = orderRepo;
        _invoiceService = invoiceService;
        _emailService = emailService;
        _rabbit = rabbit;
        _logger = logger;
    }

    [HttpPost("create-paypal-order")]
    [HttpPost("create-order")]
    public async Task<IActionResult> CreateOrder([FromBody] t_PaymentVerify request)
    {
        try
        {
            if (request?.Cart == null || request.Cart.Count == 0)
                return BadRequest(new { success = false, message = "Cart cannot be empty." });

            // Use TotalAmount from frontend (subtotal + 2% platform fee)
            // Fallback to cart sum if not provided
            decimal total = request.TotalAmount > 0
                ? request.TotalAmount
                : request.Cart.Sum(x => x.Price);

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

            bool isCaptured = await _paypalService.CaptureOrderAsync(model.PaypalOrderId!);
            Console.WriteLine(isCaptured);

            if (!isCaptured)
                return BadRequest(new { success = false, message = "Payment capture failed." });

            int buyerId = GetBuyerIdFromContext();
            int orderId = await _paymentRepo.ProcessFullPaymentAsync(buyerId, model);
            Console.WriteLine(orderId);

            if (orderId > 0)
            {
                t_OrderDetail? orderDetail = null;

                try
                {
                    orderDetail = await _orderRepo.GetOrderDetailAsync(orderId, buyerId);
                }
                catch (Exception orderDetailEx)
                {
                    _logger.LogError(orderDetailEx, "Failed to load order details for Order {OrderId}", orderId);
                }

                await PublishPaymentNotificationsAsync(buyerId, orderId, model, orderDetail);

                try
                {
                    if (orderDetail != null)
                    {
                        Console.WriteLine("Buyer Email: " + orderDetail.BuyerEmail);
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

                        string logoPath = Path.GetFullPath(Path.Combine(
                            Directory.GetCurrentDirectory(),
                            "..",
                            "mvc",
                            "wwwroot",
                            "images",
                            "Artify-Logos.png"));

                        _logger.LogInformation("Sending email to {Email}", orderDetail.BuyerEmail);

                        // Send email
                        // await _emailService.SendEmailAsync(
                        //     orderDetail.BuyerEmail,
                        //     "Order Confirmation - Artify Gallery",
                        //     body,
                        //     logoPath,
                        //     pdf,
                        //     $"Invoice_{orderId}.pdf");

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

    private async Task PublishPaymentNotificationsAsync(
        int buyerId,
        int orderId,
        t_PaymentVerify model,
        t_OrderDetail? orderDetail)
    {
        var totalAmount = orderDetail?.TotalAmount ?? model.Cart.Sum(x => x.Price);
        var currency = string.IsNullOrWhiteSpace(model.Currency) ? "USD" : model.Currency;

        try
        {
            await _rabbit.PublishPaymentNotificationAsync(
                buyerId,
                orderId,
                totalAmount,
                currency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish buyer payment notification for Order {OrderId}", orderId);
        }

        if (orderDetail == null)
            return;

        try
        {
            var artworkCount = orderDetail.Items.Count > 0 ? orderDetail.Items.Count : model.Cart.Count;

            await _rabbit.PublishAdminPaymentNotificationAsync(
                orderId: orderId,
                buyerId: buyerId,
                buyerName: orderDetail.BuyerName,
                amount: totalAmount,
                currency: currency,
                artworkCount: artworkCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish admin payment notification for Order {OrderId}", orderId);
        }

        var artistOrders = orderDetail.Items
            .Where(item => item.ArtistId > 0)
            .GroupBy(item => item.ArtistId);

        foreach (var artistOrder in artistOrders)
        {
            var artistItems = artistOrder.ToList();
            var artistTotal = artistItems.Sum(item => item.Price);
            var artworkSummary = BuildArtworkSummary(artistItems);

            try
            {
                await _rabbit.PublishArtistPaymentNotificationAsync(
                    artistId: artistOrder.Key,
                    orderId: orderId,
                    amount: artistTotal,
                    currency: currency,
                    artworkCount: artistItems.Count,
                    artworkSummary: artworkSummary);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to publish artist payment notification for Order {OrderId} and Artist {ArtistId}",
                    orderId,
                    artistOrder.Key);
            }
        }
    }

    private static string BuildArtworkSummary(IReadOnlyList<t_OrderItemDetail> items)
    {
        if (items.Count == 0)
            return "New order received";

        if (items.Count == 1)
            return items[0].Title;

        var titles = items
            .Select(item => item.Title)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        if (titles.Count == 0)
            return "New order received";

        return items.Count > titles.Count
            ? $"{string.Join(", ", titles)} +{items.Count - titles.Count} more"
            : string.Join(", ", titles);
    }
}
