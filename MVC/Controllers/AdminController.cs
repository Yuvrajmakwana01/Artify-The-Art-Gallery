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
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("AdminToken")))
                return RedirectToAction("Artworks", "AdminArtworkModerationMvc");

            return View();
        }

        // ── POST /Admin/SetSession ────────────────────────────────────────────
        // Called by Login.cshtml after the API confirms credentials.
        // Saves the JWT into server-side Session so C# controllers can guard pages.
        [HttpPost]
        public IActionResult SetSession([FromBody] AdminSessionDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Token))
                return Json(new { success = false });

            HttpContext.Session.SetString("AdminToken",  dto.Token);
            HttpContext.Session.SetString("AdminName",   dto.AdminName  ?? "Admin");
            HttpContext.Session.SetString("AdminEmail",  dto.AdminEmail ?? "");
            HttpContext.Session.SetString("AdminId",     dto.AdminId.ToString());

            return Json(new { success = true });
        }

        // ── GET /Admin/Index ──────────────────────────────────────────────────
        // Server-side guard: no session = redirect before any HTML is sent
        public IActionResult Index()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AdminToken")))
                return RedirectToAction("Login", "Admin");

            return View();
        }

        public IActionResult PaymentDetails()
        // ── GET /Admin/Logout ─────────────────────────────────────────────────
        // Clears server-side session. localStorage is cleared by _AdminLayout JS.
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Admin");
        }

        public IActionResult Categories()
        {
            return View();
        }

        public IActionResult Artists()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        public IActionResult Users()
        {
            return View();
        }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View("Error!");
    }

    // DTO received from Login.cshtml SetSession AJAX call
    public class AdminSessionDto
    {
        public string Token      { get; set; } = "";
        public string AdminName  { get; set; } = "";
        public string AdminEmail { get; set; } = "";
        public int    AdminId    { get; set; }
    }
}
