using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Repository.Models;
using Repository.Interfaces;
using Repository.Services;

namespace Repository.Services
{
    public class AdminArtworkService
    {
        private readonly IAdminArtworkInterface _repo;
        private readonly RedisService           _redis;
        // private readonly RabbitMQProducer       _mq;
        private readonly EmailServices          _email;
        private readonly ILogger<AdminArtworkService> _logger;

        public AdminArtworkService(
            IAdminArtworkInterface       repo,
            RedisService                 redis,
            // RabbitMQProducer             mq,
            EmailServices                email,
            ILogger<AdminArtworkService> logger)
        {
            _repo   = repo;
            _redis  = redis;
            // _mq     = mq;
            _email  = email;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────
        //  GET ARTWORKS  (Redis → PostgreSQL)
        // ─────────────────────────────────────────────────────────────────

        public async Task<PagedResult<ArtworkModel>> GetArtworksAsync(
            string? status, int page, int pageSize)
        {
            string cacheKey = RedisService.ArtworkCacheKey(status ?? "all", page, pageSize);

            var cached = await _redis.GetAsync<PagedResult<ArtworkModel>>(cacheKey);
            if (cached != null)
                return cached;

            var result = await _repo.GetArtworksAsync(status, page, pageSize);
            await _redis.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));

            return result;
        }

        // ─────────────────────────────────────────────────────────────────
        //  APPROVE
        // ─────────────────────────────────────────────────────────────────

        public async Task ApproveArtworkAsync(int artworkId, string? adminNote)
        {
            var artwork = await _repo.GetArtworkByIdAsync(artworkId)
                ?? throw new KeyNotFoundException($"Artwork {artworkId} not found.");

            // 1. Update DB status
            await _repo.UpdateArtworkStatusAsync(artworkId, "Approved", adminNote ?? string.Empty);

            // 2. Reset artist rejection counter
            await _repo.ResetRejectedCountAsync(artwork.c_ArtistId);

            // 3. Notify artist via RabbitMQ (in-app notification)
            // _mq.SendArtworkNotification(new ArtworkNotificationMessage
            // {
            //     c_ArtistId = artwork.c_ArtistId,
            //     c_Title    = artwork.c_Title,
            //     c_Message  = $"Your artwork '{artwork.c_Title}' has been approved and is now live!",
            //     c_Type     = "APPROVED"
            // });

            // 4. Send approval email to artist
            await SendModerationEmailAsync(
                artistId:    artwork.c_ArtistId,
                artistName:  artwork.c_ArtistName,
                artworkTitle: artwork.c_Title,
                categoryName: artwork.c_CategoryName,
                isApproved:  true,
                adminNote:   adminNote ?? string.Empty);

            // 5. Bust Redis cache
            await _redis.ClearAdminArtworkCacheAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        //  REJECT
        // ─────────────────────────────────────────────────────────────────

        public async Task RejectArtworkAsync(int artworkId, string adminNote)
        {
            var artwork = await _repo.GetArtworkByIdAsync(artworkId)
                ?? throw new KeyNotFoundException($"Artwork {artworkId} not found.");

            // 1. Update DB status
            await _repo.UpdateArtworkStatusAsync(artworkId, "Rejected", adminNote);

            // 2. Increment rejection count
            int rejectCount = await _repo.IncrementRejectedCountAsync(artwork.c_ArtistId);

            // 3. Block artist if over threshold (>3 rejections)
            if (rejectCount > 3)
                await _repo.BlockArtistAsync(artwork.c_ArtistId);

            // 4. Build RabbitMQ notification message
            string blockWarning = rejectCount > 3
                ? " Your account has been temporarily blocked for 15 days."
                : string.Empty;

            // _mq.SendArtworkNotification(new ArtworkNotificationMessage
            // {
            //     c_ArtistId = artwork.c_ArtistId,
            //     c_Title    = artwork.c_Title,
            //     c_Message  = $"Your artwork '{artwork.c_Title}' was rejected. Reason: {adminNote}.{blockWarning}",
            //     c_Type     = "REJECTED"
            // });

            // 5. Send rejection email to artist
            //    Append block warning to the admin note so the artist sees it in email too
            string fullNote = rejectCount > 3
                ? $"{adminNote}\n\n⚠️ Your account has been temporarily blocked for 15 days due to multiple rejections."
                : adminNote;

            await SendModerationEmailAsync(
                artistId:    artwork.c_ArtistId,
                artistName:  artwork.c_ArtistName,
                artworkTitle: artwork.c_Title,
                categoryName: artwork.c_CategoryName,
                isApproved:  false,
                adminNote:   fullNote);

            // 6. Bust Redis cache
            await _redis.ClearAdminArtworkCacheAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        //  PRIVATE — fetch artist email + send moderation email
        // ─────────────────────────────────────────────────────────────────

        private async Task SendModerationEmailAsync(
            int    artistId,
            string artistName,
            string artworkTitle,
            string categoryName,
            bool   isApproved,
            string adminNote)
        {
            try
            {
                // Fetch artist email from DB
                string? artistEmail = await _repo.GetArtistEmailAsync(artistId);

                if (string.IsNullOrWhiteSpace(artistEmail))
                {
                    _logger.LogWarning(
                        "Moderation email skipped — no email found for artist {ArtistId}.", artistId);
                    return;
                }

                await _email.SendArtworkModerationEmailAsync(
                    toEmail:      artistEmail,
                    artistName:   artistName,
                    artworkTitle: artworkTitle,
                    categoryName: categoryName,
                    isApproved:   isApproved,
                    adminNote:    adminNote);

                _logger.LogInformation(
                    "Moderation email ({Status}) sent to {Email} for artwork '{Title}'.",
                    isApproved ? "APPROVED" : "REJECTED",
                    artistEmail,
                    artworkTitle);
            }
            catch (Exception ex)
            {
                // Email failure must never break the moderation workflow.
                // Log the error and continue — DB is already updated.
                _logger.LogError(ex,
                    "Failed to send moderation email to artist {ArtistId} for artwork '{Title}'.",
                    artistId, artworkTitle);
            }
        }
    }
}