namespace Repository.Models;

public class t_BuyerUiArtwork
{
    public int    ArtworkId      { get; set; }
    public int    ArtistId       { get; set; }
    public int    CategoryId     { get; set; }
    public string Title          { get; set; } = string.Empty;
    public string? Description   { get; set; }
    public decimal Price         { get; set; }
    public string? PreviewPath   { get; set; }
    public string? OriginalPath  { get; set; }
    public string ApprovalStatus { get; set; } = "Pending";
    public DateTime CreatedAt    { get; set; }
    public int LikesCount        { get; set; }
    public int SellCount         { get; set; }

    // Joined from t_artist_profile
    public string ArtistName     { get; set; } = string.Empty;

    // Joined from t_category
    public string CategoryName   { get; set; } = string.Empty;
}
