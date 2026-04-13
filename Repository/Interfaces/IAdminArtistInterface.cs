using Repository.Models;

namespace Repository.Interfaces;

public interface IAdminArtistInterface
{
    Task<List<AdminArtistDto>> GetArtistsAsync(string? search, string? status);
    Task<AdminArtistStatsDto> GetArtistStatsAsync();
    Task<AdminArtistDto?> GetArtistByIdAsync(int artistId);
    Task<int> AddArtistAsync(AdminArtistUpsertRequest request);
    Task<bool> UpdateArtistAsync(int artistId, AdminArtistUpsertRequest request);
    Task<bool> DeleteArtistAsync(int artistId);
}
