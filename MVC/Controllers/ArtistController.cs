using Microsoft.AspNetCore.Mvc;
using Repository.Models;

namespace MVC.Controllers;

public class ArtistController : Controller
{
    private IActionResult? Guard()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("ArtistToken")))
            return RedirectToAction("Login");

        return null;
    }


    public IActionResult Index()
    {
        return View();
    }


    public IActionResult Dashboard()
    {
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


    public IActionResult MyProfile()
    {
        return View();
    }


    public async Task<IActionResult> ManageProfile()
{
    // 1. Are you getting the ID correctly?
    var artistId = HttpContext.Session.GetInt32("ArtistId");
    if (artistId == null)
    {
        return RedirectToAction("Login");
    }
    return View();
}


    public IActionResult Sales()
    {
        return View();
    }


    
    public IActionResult Sales_Earnings()
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



}