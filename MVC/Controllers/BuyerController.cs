using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MVC.Controllers
{
    // [Route("[controller]")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    // [SessionAuthorize]
    public class BuyerController : Controller
    {
        private readonly ILogger<BuyerController> _logger;

        private HttpClient GetClient()
        {
            var client = new HttpClient();
            var token = HttpContext.Request.Cookies["token"];

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            return client; // ✅ Exception mat throw karo
        }


        private bool IsLoggedIn()
        {
            return HttpContext.Request.Cookies["token"] != null;
        }

        public BuyerController(ILogger<BuyerController> logger)
        {
            _logger = logger;
        }

        public IActionResult ChangePassword()
        {
            return View();
        }

        public IActionResult Profile()
        {
            ViewData["InitialSection"] = "profile";
            ViewData["Title"] = "Profile";
            return View("Dashboard");
        }
        public IActionResult EditProfile()
        {
            return View();
        }

        public IActionResult Orders()
        {
            ViewData["InitialSection"] = "orders";
            ViewData["Title"] = "Order History";
            return View("Dashboard");
        }

        public IActionResult Wishlist()
        {
            ViewData["InitialSection"] = "wishlist";
            ViewData["Title"] = "Wishlist";
            return View("Dashboard");
        }

        public IActionResult Downloads()
        {
            ViewData["InitialSection"] = "downloads";
            ViewData["Title"] = "Download Arts";
            return View("Dashboard");
        }

        public IActionResult Settings()
        {
            ViewData["InitialSection"] = "settings";
            ViewData["Title"] = "Settings";
            return View("Dashboard");
        }

        public ActionResult OrderDetail(string id)
        {
            return View();
        }

        public ActionResult Dashboard()
        {
            return View();
        }
        public ActionResult ViewArts()
        {
            return View();
        }

        // [HttpGet("/Buyer/ExploreArt")]
        public IActionResult ExploreArt([FromQuery] string? query)
        {
            // var token = HttpContext.Request.Cookies["token"]; // OR check localStorage via JS

            // if (string.IsNullOrEmpty(token))
            // {
            //     return RedirectToAction("UserLogin", "Auth");
            // }

            ViewData["SearchQuery"] = query ?? string.Empty;
            return View();
        }
        public IActionResult Index()
        {
            return RedirectToAction(nameof(ExploreArt));
        }

        // [HttpGet("Buyer/ArtworkDetail/{id:int}")]
        public IActionResult ArtworkDetail(int id, [FromServices] IConfiguration configuration)
        {
            ViewData["ArtworkId"] = id;
            ViewData["ApiBaseUrl"] = configuration["ApiBaseUrl"] ?? string.Empty;
            return View("ArtworkDetail");
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}
