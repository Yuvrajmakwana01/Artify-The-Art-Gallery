using Microsoft.AspNetCore.Mvc;

namespace MVC.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IConfiguration _configuration;

        public PaymentController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private void SetPaymentConfig()
        {
            ViewData["PayPalClientId"] = _configuration["PayPal:ClientId"] ?? string.Empty;
            ViewData["PayPalCurrency"] = _configuration["PayPal:Currency"] ?? "USD";
            ViewData["PaymentApiBaseUrl"] = _configuration["ApiBaseUrl"] ?? string.Empty;
        }

        public IActionResult Index()
        {
            SetPaymentConfig();
            return View("Payment");
        }

        public IActionResult Checkout()
        {
            SetPaymentConfig();
            return View("Checkout");
        }
    }
}
