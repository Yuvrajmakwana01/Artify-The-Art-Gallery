// ────────────────────────────────────────────────────────────────────────
// API/Controllers/DownloadApiController.cs
//
// Max 5 downloads per buyer per artwork using existing t_download_log.
// No t_download_token table needed.
// ────────────────────────────────────────────────────────────────────────
using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/download")]
public class DownloadApiController : ControllerBase
{
    private readonly IOrderInterface _orderRepo;
    private readonly ILogger<DownloadApiController> _logger;
    private const int MAX_DOWNLOADS = 2;

    public DownloadApiController(
        IOrderInterface orderRepo,
        ILogger<DownloadApiController> logger)
    {
        _orderRepo = orderRepo;
        _logger    = logger;
    }

    // ── DOWNLOAD STATUS ───────────────────────────────────────────────────
    [HttpGet("status/{artworkId:int}")]
    public async Task<IActionResult> GetStatus(int artworkId)
    {
        int buyerId = GetBuyerId();

        bool owns = await _orderRepo.BuyerOwnsArtworkAsync(buyerId, artworkId);
        if (!owns)
            return NotFound(new { success = false, message = "You have not purchased this artwork." });

        int count = await _orderRepo.GetDownloadCountAsync(buyerId, artworkId);

        return Ok(new
        {
            success       = true,
            artworkId,
            downloadCount = count,
            downloadsLeft = Math.Max(0, MAX_DOWNLOADS - count),
            canDownload   = count < MAX_DOWNLOADS,
            maxDownloads  = MAX_DOWNLOADS
        });
    }

    // ── DOWNLOAD ARTWORK ──────────────────────────────────────────────────
    [HttpGet("{artworkId:int}")]
    public async Task<IActionResult> Download(int artworkId, [FromQuery] int orderId)
    {
        int buyerId = GetBuyerId();

        // 1. Verify buyer owns this artwork
        bool owns = await _orderRepo.BuyerOwnsArtworkAsync(buyerId, artworkId);
        if (!owns)
        {
            _logger.LogWarning("Unauthorized download: Buyer {BuyerId} → Artwork {ArtworkId}", buyerId, artworkId);
            return Forbid();
        }

        // 2. Enforce 5-download limit
        int count = await _orderRepo.GetDownloadCountAsync(buyerId, artworkId);
        if (count >= MAX_DOWNLOADS)
        {
            return BadRequest(new
            {
                success       = false,
                limitReached  = true,
                message       = $"You have reached the maximum of {MAX_DOWNLOADS} downloads for this artwork.",
                downloadCount = count,
                maxDownloads  = MAX_DOWNLOADS
            });
        }

        // 3. Get file path
        string? filePath = await _orderRepo.GetOriginalPathAsync(artworkId);
        if (string.IsNullOrEmpty(filePath))
        {
            _logger.LogError("No original path for Artwork {ArtworkId}", artworkId);
            return NotFound(new { success = false, message = "File not found. Please contact support@artify.in" });
        }

        var absolutePath = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(Directory.GetCurrentDirectory(), filePath);

        if (!System.IO.File.Exists(absolutePath))
        {
            _logger.LogError("File missing on disk: {Path}", absolutePath);
            return NotFound(new { success = false, message = "Artwork file not found. Please contact support@artify.in" });
        }

        // 4. Log download BEFORE serving (counts it)
        await _orderRepo.LogDownloadAsync(orderId, artworkId, buyerId);

        int newCount  = count + 1;
        int remaining = MAX_DOWNLOADS - newCount;

        _logger.LogInformation(
            "Download #{Count}/{Max} — Buyer {BuyerId} → Artwork {ArtworkId}",
            newCount, MAX_DOWNLOADS, buyerId, artworkId);

        // 5. Add download info headers
        Response.Headers.Append("X-Downloads-Used",      newCount.ToString());
        Response.Headers.Append("X-Downloads-Remaining", remaining.ToString());
        Response.Headers.Append("X-Downloads-Max",       MAX_DOWNLOADS.ToString());

        // 6. Serve file
        var ext      = Path.GetExtension(absolutePath).ToLowerInvariant();
        var mime     = GetMimeType(ext);
        var fileName = $"Artify_{artworkId}{ext}";

        return PhysicalFile(absolutePath, mime, fileName);
    }

    private static string GetMimeType(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"            => "image/png",
        ".gif"            => "image/gif",
        ".webp"           => "image/webp",
        ".tiff"           => "image/tiff",
        ".pdf"            => "application/pdf",
        ".zip"            => "application/zip",
        _                 => "application/octet-stream"
    };

    private int GetBuyerId()
    {
        var claim = User.FindFirst("sub")?.Value
                 ?? User.FindFirst("user_id")?.Value;
        return int.TryParse(claim, out int id) ? id : 1;
    }
}
