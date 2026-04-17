using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImageApiController : ControllerBase
    {
        // Note: Production mein Access Key ko appsettings.json mein rakhein
        private readonly string _accessKey = "gf2MfzgSNjKR4lf_m1Z5aWr57zdSjCSMXPM6gOdiuEI";

        [HttpGet("art-background")]
        public async Task<IActionResult> GetArtImages()
        {
            // Jab tak Rate Limit reset nahi hoti, ye dummy data return karein
    // var dummyData = new[] {
    //     new { urls = new { regular = "https://images.unsplash.com/photo-1579783902614-a3fb3927b6a5" } },
    //     new { urls = new { regular = "https://images.unsplash.com/photo-1549490349-8643362247b5" } },
    //     new { urls = new { regular = "https://images.unsplash.com/photo-1578301978693-85fa9c0320b9" } }
    // };
            // return Ok(dummyData);
            try 
            {
                using var client = new HttpClient();

                // 1. Safe Search Queries
                string safeQueries = "digital-art,oil-painting,nature,abstract-art,scenery, flower, landscape, portrait, modern-art, surrealism, impressionism, art-gallery, art-museum, art-collection, art-exhibition, art-studio, art-workshop, art-festival, art-event, art-artist, art-creation";
                
                // 2. URL Fix: '&' ko direct use karein aur 'content_filter=high' add karein vulgar images rokne ke liye
                // 'count=30' ko badha kar 40-50 bhi kar sakte hain agar grid khali dikh raha hai
                string url = $"https://api.unsplash.com/photos/random?client_id={_accessKey}&query={safeQueries}&count=100&content_filter=high";

                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Ok(content);
                }
                else
                {
                    // Agar error aaye toh status code check karne ke liye
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, $"Unsplash API Error: {errorDetails}");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }
    }
}
