using Microsoft.AspNetCore.Mvc;

namespace MVC.Controllers;

public class ArtistController : Controller
{
    private IActionResult? Guard()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("ArtistToken")))
            return RedirectToAction("Login");

        return null;
    }

    
    public IActionResult Dashboard()
    {
        var g = Guard();
        if (g != null) return g;
        return View();
    }

    public IActionResult Register()
        {
            return View();
        }

        public IActionResult Login()
        {
            return View();
        }


        public IActionResult Upload()
        {
            return View();
        }


        public IActionResult Gallery()
        {
            return View();
        }


        public IActionResult Settings()
        {
            return View();
        }

        public IActionResult Profile()
        {
            return View();
        }


        public IActionResult Logout()
        {
            // Clear all session data
            HttpContext.Session.Clear();

            return RedirectToAction("Login", "Artist");
        }
    public IActionResult EditProfile() => View();
    public IActionResult Sales() => View();

   
}