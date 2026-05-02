using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Repository.Interfaces;
using Repository.Models;

namespace Repository.Services
{
    public class AdminArtworkService
    {
        private readonly IAdminArtworkInterface _repo;
        private readonly RedisService _redis;
        private readonly RabbitService _rabbit;
        private readonly EmailService _email;
        private readonly ILogger<AdminArtworkService> _logger;

        public AdminArtworkService(
            IAdminArtworkInterface repo,
            RedisService redis,
            RabbitService rabbit,
            EmailService email,
            ILogger<AdminArtworkService> logger)
        {
            _repo = repo;
            _redis = redis;
            _rabbit = rabbit;
            _email = email;
            _logger = logger;
        }

        public async Task<PagedResult<ArtworkModel>> GetArtworksAsync(
            string? status, int page, int pageSize)
        {
            // Tiny 1-item requests are only used to fetch tab counts in the admin UI.
            // Skipping Redis here avoids noisy cache keys like *_1_1.
            if (pageSize <= 1)
            {
                return await _repo.GetArtworksAsync(status, page, pageSize);
            }

            string cacheKey = RedisService.ArtworkCacheKey(status ?? "all", page, pageSize);

            var cached = await _redis.GetAsync<PagedResult<ArtworkModel>>(cacheKey);
            if (cached != null)
                return cached;

            var result = await _repo.GetArtworksAsync(status, page, pageSize);
            await _redis.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));

            return result;
        }

        public async Task ApproveArtworkAsync(int artworkId, string? adminNote)
        {
            var artwork = await _repo.GetArtworkByIdAsync(artworkId)
                ?? throw new KeyNotFoundException($"Artwork {artworkId} not found.");

            try
            {
                await _repo.UpdateArtworkStatusAsync(artworkId, "Approved", adminNote ?? string.Empty);
                await _repo.ResetRejectedCountAsync(artwork.c_ArtistId);

                await PublishArtworkNotificationAsync(
                    artwork,
                    "approved",
                    $"Your artwork '{artwork.c_Title}' has been approved and is now live!");

                await SendModerationEmailAsync(
                    artistId: artwork.c_ArtistId,
                    artistName: artwork.c_ArtistName,
                    artworkTitle: artwork.c_Title,
                    categoryName: artwork.c_CategoryName,
                    isApproved: true,
                    adminNote: adminNote ?? string.Empty);
            }
            finally
            {
                await _redis.ClearAdminArtworkCacheAsync();
            }
        }

        public async Task RejectArtworkAsync(int artworkId, string adminNote)
        {
            var artwork = await _repo.GetArtworkByIdAsync(artworkId)
                ?? throw new KeyNotFoundException($"Artwork {artworkId} not found.");

            try
            {
                await _repo.UpdateArtworkStatusAsync(artworkId, "Rejected", adminNote);

                int rejectCount = await _repo.IncrementRejectedCountAsync(artwork.c_ArtistId);
                bool shouldBlock = rejectCount > 3;

                if (shouldBlock)
                    await _repo.BlockArtistAsync(artwork.c_ArtistId);

                string blockWarning = shouldBlock
                    ? " Your account has been temporarily inactive for 5 minutes."
                    : string.Empty;

                await PublishArtworkNotificationAsync(
                    artwork,
                    "rejected",
                    $"Your artwork '{artwork.c_Title}' was rejected. Reason: {adminNote}.{blockWarning}");

                string fullNote = shouldBlock
                    ? $"{adminNote}\n\nYour account has been temporarily inactive for 5 minutes due to more than 3 artwork rejections."
                    : adminNote;

                await SendModerationEmailAsync(
                    artistId: artwork.c_ArtistId,
                    artistName: artwork.c_ArtistName,
                    artworkTitle: artwork.c_Title,
                    categoryName: artwork.c_CategoryName,
                    isApproved: false,
                    adminNote: fullNote);
            }
            finally
            {
                await _redis.ClearAdminArtworkCacheAsync();
            }
        }

        private async Task SendModerationEmailAsync(
            int artistId,
            string artistName,
            string artworkTitle,
            string categoryName,
            bool isApproved,
            string adminNote)
        {
            try
            {
                string? artistEmail = await _repo.GetArtistEmailAsync(artistId);

                if (string.IsNullOrWhiteSpace(artistEmail))
                {
                    _logger.LogWarning(
                        "Moderation email skipped - no email found for artist {ArtistId}.", artistId);
                    return;
                }

                // await _email.SendArtworkModerationEmailAsync(
                //     toEmail: artistEmail,
                //     artistName: artistName,
                //     artworkTitle: artworkTitle,
                //     categoryName: categoryName,
                //     isApproved: isApproved,
                //     adminNote: adminNote);

                _logger.LogInformation(
                    "Moderation email ({Status}) sent to {Email} for artwork '{Title}'.",
                    isApproved ? "APPROVED" : "REJECTED",
                    artistEmail,
                    artworkTitle);
            }
            catch (Exception ex)
            {
                // Email failure must never break the moderation workflow.
                _logger.LogError(ex,
                    "Failed to send moderation email to artist {ArtistId} for artwork '{Title}'.",
                    artistId, artworkTitle);
            }
        }

        private async Task PublishArtworkNotificationAsync(
            ArtworkModel artwork,
            string status,
            string message)
        {
            try
            {
                await _rabbit.PublishArtworkModerationNotificationAsync(
                    artistId: artwork.c_ArtistId,
                    artworkTitle: artwork.c_Title,
                    message: message,
                    status: status);
            }
            catch (Exception ex)
            {
                // Notification failures should not roll back moderation decisions.
                _logger.LogError(
                    ex,
                    "Failed to publish {Status} notification for artwork {ArtworkId}.",
                    status,
                    artwork.c_ArtworkId);
            }
        }
    }
}