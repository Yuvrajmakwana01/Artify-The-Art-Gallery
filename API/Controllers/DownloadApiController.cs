// ────────────────────────────────────────────────────────────────────────
// API/Controllers/DownloadApiController.cs
// ────────────────────────────────────────────────────────────────────────
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;
using System.Security.Claims;

namespace API.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class DownloadApiController : ControllerBase
{
    private readonly IOrderInterface _orderRepo;
    private readonly ILogger<DownloadApiController> _logger;
    private readonly HttpClient _httpClient;
    private const int MAX_DOWNLOADS = 2;

    public DownloadApiController(
        IOrderInterface orderRepo,
        ILogger<DownloadApiController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _orderRepo  = orderRepo;
        _logger     = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    // ── DEBUG ENDPOINT ────────────────────────────────────────────────────
    [HttpGet("debug/{artworkId:int}")]
    public async Task<IActionResult> Debug(int artworkId, [FromQuery] int orderId)
    {
        int buyerId = GetBuyerId();

        bool owns  = buyerId > 0 && await _orderRepo.BuyerOwnsArtworkAsync(buyerId, artworkId);
        int  count = owns ? await _orderRepo.GetDownloadCountAsync(buyerId, artworkId) : -1;

        string? filePath  = await _orderRepo.GetOriginalPathAsync(artworkId);
        bool    isUrl     = !string.IsNullOrEmpty(filePath) && filePath.StartsWith("http", StringComparison.OrdinalIgnoreCase);

        string? absolutePath = null;
        bool    fileExists   = false;

        if (!string.IsNullOrEmpty(filePath) && !isUrl)
        {
            absolutePath = Path.IsPathRooted(filePath)
                ? filePath
                : Path.Combine(Directory.GetCurrentDirectory(), filePath);
            fileExists = System.IO.File.Exists(absolutePath);
        }

        return Ok(new
        {
            step1_buyerIdResolved  = buyerId,
            step1_allClaims        = User.Claims.Select(c => new { c.Type, c.Value }),
            step2_buyerOwnsArtwork = owns,
            step3_downloadCount    = count,
            step3_canDownload      = count < MAX_DOWNLOADS,
            step4_filePathInDb     = filePath,
            step4_isCloudinaryUrl  = isUrl,
            step4_absolutePath     = absolutePath,
            step4_fileExistsOnDisk = fileExists,
            artworkId,
            orderId
        });
    }

    // ── DOWNLOAD STATUS ───────────────────────────────────────────────────
    [HttpGet("status/{artworkId:int}")]
    public async Task<IActionResult> GetStatus(int artworkId)
    {
        int buyerId = GetBuyerId();
        if (buyerId <= 0)
            return Unauthorized(new { success = false, message = "Invalid token — buyer ID not found in claims." });

        bool owns = await _orderRepo.BuyerOwnsArtworkAsync(buyerId, artworkId);
        if (!owns)
            return NotFound(new { success = false, message = $"Buyer {buyerId} does not own artwork {artworkId}." });

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
        if (buyerId <= 0)
        {
            _logger.LogWarning("Download failed — no buyerId in token. Claims: {Claims}",
                string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
            return Unauthorized(new { success = false, message = "Invalid token." });
        }

        // 1. Verify ownership
        bool owns = await _orderRepo.BuyerOwnsArtworkAsync(buyerId, artworkId);
        if (!owns)
        {
            _logger.LogWarning("Buyer {BuyerId} does not own Artwork {ArtworkId}", buyerId, artworkId);
            return StatusCode(403, new { success = false, message = $"Buyer {buyerId} has not purchased artwork {artworkId}." });
        }

        // 2. Enforce download limit
        int count = await _orderRepo.GetDownloadCountAsync(buyerId, artworkId);
        if (count >= MAX_DOWNLOADS)
        {
            return BadRequest(new
            {
                success       = false,
                limitReached  = true,
                message       = $"Maximum {MAX_DOWNLOADS} downloads reached.",
                downloadCount = count,
                maxDownloads  = MAX_DOWNLOADS
            });
        }

        // 3. Get file path
        string? filePath = await _orderRepo.GetOriginalPathAsync(artworkId);
        if (string.IsNullOrEmpty(filePath))
        {
            _logger.LogError("No file path in DB for Artwork {ArtworkId}", artworkId);
            return NotFound(new { success = false, message = "File path not found in database." });
        }

        // 4. Log download BEFORE streaming (so limit is enforced even if stream fails)
        await _orderRepo.LogDownloadAsync(orderId, artworkId, buyerId);

        int newCount  = count + 1;
        int remaining = MAX_DOWNLOADS - newCount;

        Response.Headers.Append("X-Downloads-Used",      newCount.ToString());
        Response.Headers.Append("X-Downloads-Remaining", remaining.ToString());
        Response.Headers.Append("X-Downloads-Max",       MAX_DOWNLOADS.ToString());

        // ── Cloudinary / remote URL ───────────────────────────────────────
        bool isUrl = filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                  || filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (isUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(filePath, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Cloudinary fetch failed ({Status}): {Url}", response.StatusCode, filePath);
                    return StatusCode(502, new { success = false, message = "Failed to fetch image from storage." });
                }

                var contentType = response.Content.Headers.ContentType?.MediaType
                               ?? GetMimeType(Path.GetExtension(new Uri(filePath).AbsolutePath).ToLowerInvariant());

                var ext      = ExtFromMime(contentType);
                var fileName = $"Artify_{artworkId}{ext}";

                var stream = await response.Content.ReadAsStreamAsync();
                return File(stream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception fetching Cloudinary URL: {Url}", filePath);
                return StatusCode(502, new { success = false, message = "Error fetching image from cloud storage." });
            }
        }

        // ── Local file path ───────────────────────────────────────────────
        var absolutePath = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(Directory.GetCurrentDirectory(), filePath);

        if (!System.IO.File.Exists(absolutePath))
        {
            _logger.LogError("File missing on disk: {Path}", absolutePath);
            return NotFound(new { success = false, message = $"File not found on disk: {absolutePath}" });
        }

        var localExt  = Path.GetExtension(absolutePath).ToLowerInvariant();
        var localMime = GetMimeType(localExt);
        var localName = $"Artify_{artworkId}{localExt}";

        return PhysicalFile(absolutePath, localMime, localName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

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

    private static string ExtFromMime(string mime) => mime switch
    {
        "image/jpeg"      => ".jpg",
        "image/png"       => ".png",
        "image/gif"       => ".gif",
        "image/webp"      => ".webp",
        "image/tiff"      => ".tiff",
        "application/pdf" => ".pdf",
        _                 => ".jpg"   // Cloudinary defaults to JPEG when unknown
    };

    private int GetBuyerId()
    {
        var claimTypes = new[]
        {
            "sub",
            "user_id",
            "userId",
            "buyerId",
            "id",
            ClaimTypes.NameIdentifier,
            ClaimTypes.Name
        };

        foreach (var type in claimTypes)
        {
            var value = User.FindFirst(type)?.Value;
            if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int id) && id > 0)
                return id;
        }

        _logger.LogWarning("BuyerId not resolved. All claims: {Claims}",
            string.Join(" | ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
        return 0;
    }
}
