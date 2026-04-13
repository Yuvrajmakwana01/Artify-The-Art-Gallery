using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repository.Models;

namespace Repository.Interfaces
{
    public interface IAdminArtworkInterface
    {
        // ── Queries ────────────────────────────────────────────────────────
        Task<ArtworkModel?> GetArtworkByIdAsync(int artworkId);

        Task<PagedResult<ArtworkModel>> GetArtworksAsync(
            string? status,
            int page,
            int pageSize);

        // ── Status mutations ───────────────────────────────────────────────
        Task UpdateArtworkStatusAsync(int artworkId, string status, string adminNote);

        // ── Artist rejection tracking ──────────────────────────────────────
        Task ResetRejectedCountAsync(int artistId);
        Task<int> IncrementRejectedCountAsync(int artistId);
        Task BlockArtistAsync(int artistId);
        Task<bool> IsArtistBlockedAsync(int artistId);
    }
}