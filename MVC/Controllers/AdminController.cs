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
    public class AdminController : Controller
    {
        private readonly ILogger<AdminController> _logger;

        public AdminController(ILogger<AdminController> logger)
        {
            _logger = logger;
        }

        // ── GET /Admin/Login ──────────────────────────────────────────────────
        // If already logged in, skip the login page entirely
        public IActionResult Login()
        {
            return View();
        }

        // ── GET /Admin/Index ──────────────────────────────────────────────────
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Payouts()
        {
            return View();
        }

         public IActionResult SalesSummary()
        {
            return View();
        }

         public IActionResult Profile()
        {
            return View();
        }

         public IActionResult EditProfile()
        {
            ViewBag.AdminId = HttpContext.Session.GetString("AdminId");
            return View();
        }

        public IActionResult Categories()
        {
            return View();
        }

        public IActionResult Artists()
        {
            return View();
        }

        public IActionResult Users()
        {
            return View();
        }

        public IActionResult ArtworksLoading()
        {
            return RedirectToAction(nameof(Artworks));
        }

        public IActionResult Artworks()
        {
            return View();
        }

        public IActionResult Orders()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View("Error!");
    }
}
