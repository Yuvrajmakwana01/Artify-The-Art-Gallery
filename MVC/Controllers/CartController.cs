// CartController.cs
using Microsoft.AspNetCore.Mvc;

namespace MVC.Controllers
{
    public class CartController : Controller
    {
        private readonly IConfiguration _configuration;

        public CartController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            ViewData["ApiBaseUrl"] = _configuration["ApiBaseUrl"] ?? string.Empty;
            return View("Cart");
        }

        [HttpGet("Cart/Detail/{id:int}")]
        public IActionResult Detail(int id)
        {
            ViewData["OrderId"] = id;
            ViewData["ApiBaseUrl"] = _configuration["ApiBaseUrl"] ?? string.Empty;
            return View("Detail");
        }
    }
}
