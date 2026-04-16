using Microsoft.AspNetCore.Mvc;

namespace MVC.Controllers
{
    public class WishlistController : Controller
    {
        private readonly IConfiguration _configuration; // ✅ REQUIRED

        public WishlistController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
       
        public IActionResult Index()
        {
            ViewData["ApiBaseUrl"] = _configuration["ApiBaseUrl"] ?? string.Empty;

            return View(); // ✅ better than View("Wishlist")
        }
    }
}