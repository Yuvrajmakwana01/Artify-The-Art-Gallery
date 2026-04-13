using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;

namespace MVC.Controllers
{
    public class AuthController : Controller
    {
        private readonly ILogger<AuthController> _logger;

        public AuthController(ILogger<AuthController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }
        public IActionResult UserForgotPassword()
        {
            ViewBag.BackgroundImageUrl = "https://yourwebsite.com/api/images/auth/forgot-password-bg";
            return View();
        }

        public IActionResult UserRegister()
        {
            ViewBag.BackgroundImageUrl = "https://yourwebsite.com/api/images/auth/register-bg";
            return View();
        }
        public IActionResult UserLogin()
        {
            ViewBag.BackgroundImageUrl = "https://yourwebsite.com/api/images/auth/login-bg";
            return View();
        }

        public IActionResult UserGoogleLogin()
        {
            // Google pe redirect karo
            var redirectUrl = Url.Action("UserGoogleCallback", "Auth");
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };


            // Yahan "Google" likhna zaroori hai taaki system JWT ko chhod kar Google handler use kare
            return Challenge(properties, "Google");
        }

        public async Task<IActionResult> UserGoogleCallback()
        {
            // 1. Google se authenticated data lein
            var result = await HttpContext.AuthenticateAsync("Cookies");
            if (!result.Succeeded) return RedirectToAction("UserLogin");

            // 2. Google ke claims nikaalo
            var claims = result.Principal.Identities.FirstOrDefault()?.Claims;
            var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var googleId = claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            using (var client = new HttpClient())
            {
                var googleData = new
                {
                    c_Email = email,        // Name match karo vm_GoogleLogin se
                    c_FullName = name,
                    c_GoogleId = googleId, 
                };

                var content = new StringContent(JsonSerializer.Serialize(googleData), Encoding.UTF8, "application/json");
                // Apni API ka sahi URL yahan daalein
                var response = await client.PostAsync("http://localhost:5183/api/AuthApi/UserGoogleLogin/", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseData = await response.Content.ReadAsStringAsync();
                    var apiResult = JsonSerializer.Deserialize<JsonElement>(responseData);

                    // API se jo JWT Token mila hai use Session mein save karo
                    // string token = apiResult.GetProperty("token").GetString();
                    // HttpContext.Session.SetString("JWToken", token);

                    // return RedirectToAction("Landingpage", "Home");

                    if (apiResult.TryGetProperty("token", out var tokenElement))
                    {
                        string? token = tokenElement.GetString();
                        if (!string.IsNullOrEmpty(token))
                        {
                            HttpContext.Session.SetString("JWToken", token);
                            return RedirectToAction("EditProfile", "Buyer");
                        }
                    }
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    // Debug karein ya console mein dekhein ki error kya hai
                    Console.WriteLine($"API Error: {errorBody}"); 
                    return Content($"API ne error diya: {errorBody}"); // Temporary check ke liye
                }
                
            }

            return RedirectToAction("UserLogin");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}