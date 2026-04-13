using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;
using Repository.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace API.Controllers;

[ApiController]
[Route("api/artist")]
public class ArtistApiController : ControllerBase
{
    private readonly IArtistInterface _artistRepo;
    private readonly IArtworkInterface _artworkRepo;
    private readonly IConfiguration _config;

    public ArtistApiController(
        IArtistInterface artistRepo,
        IArtworkInterface artworkRepo,
        IConfiguration config)
    {
        _artistRepo = artistRepo;
        _artworkRepo = artworkRepo;
        _config = config;
    }

    // ================= DASHBOARD =================
    [HttpGet("dashboard/{artistId}")]
    public async Task<IActionResult> GetDashboard(int artistId)
    {
        var data = await _artistRepo.GetDashboardData(artistId);
        return data == null ? NotFound() : Ok(data);
    }

    // ================= REGISTER =================
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromForm] t_Artist user)
    {
        var status = await _artistRepo.Register(user);
        return status == 1
            ? Ok(new { success = true })
            : Ok(new { success = false });
    }

    // ================= LOGIN =================
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] vm_Login user)
    {
        Console.WriteLine("Data" + user.c_Email + user.c_Password);
      
        var data = await _artistRepo.Login(user);

        if (data == null)
            return Ok(new { success = false });

        var claims = new[]
        {
            new Claim("UserId", data.c_User_Id.ToString()),
            new Claim("UserName", data.c_UserName)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"])
        );

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return Ok(new
        {
            success = true,
            token = new JwtSecurityTokenHandler().WriteToken(token),
            data
        });
    }

    // ================= PROFILE =================
    [HttpGet("profile/{id}")]
    public async Task<IActionResult> GetProfile(int id)
    {
        var data = await _artistRepo.GetArtistById(id);
        return Ok(data);
    }

    [HttpPost("profile")]
    public async Task<IActionResult> EditProfile([FromBody] t_ArtistProfile model)
    {
        var result = await _artistRepo.EditArtistProfile(model);
        return Ok(result);
    }

    // ================= UPLOAD =================
    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] t_Artwork art)
    {
        var status = await _artworkRepo.UploadArtwork(art);
        return status > 0 ? Ok() : BadRequest();
    }

    // ================= ARTWORK =================
    [HttpGet("artworks")]
    public async Task<IActionResult> GetAll()
    {
        var data = await _artworkRepo.GetAllArtworks();
        return Ok(data);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var data = await _artworkRepo.GetCategories();
        return Ok(data);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok();
    }
}