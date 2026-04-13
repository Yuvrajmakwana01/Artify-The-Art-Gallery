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

    public IActionResult Login() => View();
    public IActionResult Register() => View();

    public IActionResult Dashboard()
    {
        var g = Guard();
        if (g != null) return g;
        return View();
    }

    public IActionResult Upload() => View();
    public IActionResult Gallery() => View();
    public IActionResult Profile() => View();
    public IActionResult EditProfile() => View();
    public IActionResult Sales() => View();

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }
}