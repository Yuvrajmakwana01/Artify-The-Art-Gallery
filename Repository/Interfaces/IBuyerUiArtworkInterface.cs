using Repository.Models;

namespace Repository.Interfaces;

public interface IBuyerUiArtworkInterface
{
    /// <summary>All approved artworks, joined with artist name and category name.</summary>
    Task<List<t_BuyerUiArtwork>> GetAllApprovedAsync();

    /// <summary>Search approved artworks. Elasticsearch is used when available; PostgreSQL remains the fallback.</summary>
    Task<List<t_BuyerUiArtwork>> SearchArtworks(string query);

    /// <summary>Single approved artwork by primary key.</summary>
    Task<t_BuyerUiArtwork?> GetByIdAsync(int artworkId);
}
