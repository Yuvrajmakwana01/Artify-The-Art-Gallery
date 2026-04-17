// ============================================================
//  MVC/Controllers/ArtistController.cs
//  Server-side route guard: checks Session ArtistId AND
//  the HttpOnly ArtistToken cookie before serving any view.
// ============================================================

using Microsoft.AspNetCore.Mvc;

namespace MVC.Controllers;

public class ArtistController : Controller
{
    // ── Shared guard: returns redirect if not authenticated ──
    // Checks BOTH session AND the HttpOnly cookie (set by API on login)
    private IActionResult? Guard()
    {
        var sessionId = HttpContext.Session.GetInt32("ArtistId");
        var hasCookie = Request.Cookies.ContainsKey("ArtistToken");

        if (sessionId == null || !hasCookie)
        {
            // Clear stale state
            HttpContext.Session.Clear();
            Response.Cookies.Delete("ArtistToken");
            return RedirectToAction("Login");
        }
        return null;
    }

    // ── PUBLIC pages ──────────────────────────────────────────
    public IActionResult Login()
    {
        // Already logged in? Skip to dashboard
        if (HttpContext.Session.GetInt32("ArtistId") != null
            && Request.Cookies.ContainsKey("ArtistToken"))
            return RedirectToAction("Dashboard");

        return View();
    }

    public IActionResult Register() => View();

    // ── PROTECTED pages ───────────────────────────────────────
    public IActionResult DashBoard()
    {
        var guard = Guard(); if (guard != null) return guard;
        return View();
    }

    public IActionResult Upload()
    {
        var guard = Guard(); if (guard != null) return guard;
        return View();
    }

    public IActionResult Gallery()
    {
        var guard = Guard(); if (guard != null) return guard;
        return View();
    }

    public IActionResult MyProfile()
    {
        var guard = Guard(); if (guard != null) return guard;
        return View();
    }

    public IActionResult Profile()
    {
        var guard = Guard(); if (guard != null) return guard;
        return View();
    }

    public IActionResult Settings()
    {
        var guard = Guard(); if (guard != null) return guard;
        return View();
    }

    public IActionResult Sales()
    {
        var guard = Guard(); if (guard != null) return guard;
        return View();
    }

    public IActionResult Sales_Earnings()
    {
        var guard = Guard(); if (guard != null) return guard;
        return View();
    }

    public IActionResult EditProfile()
    {
        var guard = Guard(); if (guard != null) return guard;
        return View();
    }

    // ── LOGOUT — clears session + cookie on MVC side ──────────
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        Response.Cookies.Delete("ArtistToken");
        // Also call API logout (fire-and-forget; don't block redirect)
        return RedirectToAction("Landingpage","Home");
    }

    // Legacy action kept for route compat
    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> ManageProfile()
    {
        var guard = Guard(); if (guard != null) return guard;
        return View();
    }


    // Called by JS after successful API login to create the MVC session
    [HttpPost]
    public IActionResult SetSession([FromBody] SessionPayload payload)
    {
        if (payload == null || payload.ArtistId <= 0)
            return BadRequest();

        HttpContext.Session.SetInt32("ArtistId", payload.ArtistId);

        // Mirror the cookie on the MVC origin
        Response.Cookies.Append("ArtistToken", payload.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return Ok(new { success = true });
    }

    public class SessionPayload
    {
        public int ArtistId { get; set; }
        public string Token { get; set; } = "";
    }
}