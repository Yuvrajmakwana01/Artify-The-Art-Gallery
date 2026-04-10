namespace MVC.Models;

public class LandingArtworkDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Likes { get; set; }
    public bool IsLiked { get; set; }
    public bool IsWishlisted { get; set; }
    public bool InCart { get; set; }
    public bool IsPurchased { get; set; }
}

public class LandingPageResponse
{
    public List<LandingArtworkDto> Artworks { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public bool IsAuthenticated { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public int PreviewLimit { get; set; }
    public int TotalArtworkCount { get; set; }
    public int WishlistCount { get; set; }
    public int CartCount { get; set; }
    public int PurchaseCount { get; set; }
}

public class ArtworkActionRequest
{
    public int ArtworkId { get; set; }
}
