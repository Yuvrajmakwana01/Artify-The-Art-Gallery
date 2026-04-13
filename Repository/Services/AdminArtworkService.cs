using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repository.Models;
using Repository.Interfaces;
using Repository.Services;

namespace Repository.Services
{
    public class AdminArtworkService
    {
        private readonly IAdminArtworkInterface _repo;
        private readonly RedisService _redis;
        private readonly RabbitMQProducer _mq;

        public AdminArtworkService(
            IAdminArtworkInterface repo,
            RedisService redis,
            RabbitMQProducer mq)
        {
            _repo = repo;
            _redis = redis;
            _mq = mq;
        }

        // ─────────────────────────────────────────────────────────────────
        //  GET ARTWORKS  (Redis → PostgreSQL)
        // ─────────────────────────────────────────────────────────────────

        public async Task<PagedResult<ArtworkModel>> GetArtworksAsync(
            string? status, int page, int pageSize)
        {
            // ✅ use the helper so prefix matches
            string cacheKey = RedisService.ArtworkCacheKey(status ?? "all", page, pageSize);
            // generates: "admin_artworks_all_1_10" — matches what ClearAdminArtworkCacheAsync scans

            // 1. Try Redis
            var cached = await _redis.GetAsync<PagedResult<ArtworkModel>>(cacheKey);
            if (cached != null)
                return cached;

            // 2. Cache miss → PostgreSQL
            var result = await _repo.GetArtworksAsync(status, page, pageSize);

            // 3. Cache for 5 minutes
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

            // 1. Update DB
            await _repo.UpdateArtworkStatusAsync(artworkId, "Approved", adminNote ?? string.Empty);

            // 2. Reset artist rejection counter
            await _repo.ResetRejectedCountAsync(artwork.c_ArtistId);

            // 3. Notify artist via RabbitMQ
            _mq.SendArtworkNotification(new ArtworkNotificationMessage
            {
                c_ArtistId = artwork.c_ArtistId,
                c_Title = artwork.c_Title,
                c_Message = $"Your artwork '{artwork.c_Title}' has been approved and is now live!",
                c_Type = "APPROVED"
            });

            // 4. Bust Redis cache
            await _redis.ClearAdminArtworkCacheAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        //  REJECT
        // ─────────────────────────────────────────────────────────────────

        public async Task RejectArtworkAsync(int artworkId, string adminNote)
        {
            var artwork = await _repo.GetArtworkByIdAsync(artworkId)
                ?? throw new KeyNotFoundException($"Artwork {artworkId} not found.");

            // 1. Update DB
            await _repo.UpdateArtworkStatusAsync(artworkId, "Rejected", adminNote);

            // 2. Increment rejection count
            int rejectCount = await _repo.IncrementRejectedCountAsync(artwork.c_ArtistId);

            // 3. Block artist if over threshold
            if (rejectCount > 3)
                await _repo.BlockArtistAsync(artwork.c_ArtistId);

            // 4. Notify artist via RabbitMQ
            string blockWarning = rejectCount > 3
                ? " Your account has been temporarily blocked for 15 days."
                : string.Empty;

            _mq.SendArtworkNotification(new ArtworkNotificationMessage
            {
                c_ArtistId = artwork.c_ArtistId,
                c_Title = artwork.c_Title,
                c_Message = $"Your artwork '{artwork.c_Title}' was rejected. Reason: {adminNote}.{blockWarning}",
                c_Type = "REJECTED"
            });

            // 5. Bust Redis cache
            await _redis.ClearAdminArtworkCacheAsync();
        }
    }
}