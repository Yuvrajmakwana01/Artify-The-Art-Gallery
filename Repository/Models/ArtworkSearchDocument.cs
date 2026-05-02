namespace Repository.Models;

public class ArtworkSearchDocument
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
}
