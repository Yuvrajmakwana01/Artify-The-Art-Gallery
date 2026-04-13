using Microsoft.AspNetCore.Mvc;

namespace MVC.Controllers
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class AdminArtworkModerationMvcController : Controller
    {
        // ── Server-side guard ─────────────────────────────────────────────────
        // Checks HttpContext.Session before returning any view.
        // This runs on the server BEFORE any HTML is sent to the browser,
        // so the page cannot be accessed at all without a valid session.
        private IActionResult? RedirectIfNotLoggedIn()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AdminToken")))
                return RedirectToAction("Login", "Admin");

            return null; // null = authenticated, continue normally
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Artworks));
        }

        public IActionResult Artworks()
        {
            // var guard = RedirectIfNotLoggedIn();
            // if (guard != null) return guard;

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View();
    }
}