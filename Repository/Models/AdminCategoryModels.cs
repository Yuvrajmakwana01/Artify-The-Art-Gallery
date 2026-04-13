namespace Repository.Models;

public class AdminCategoryDto
{
    public int CategoryId { get; set; }
    public string CategoryIcon { get; set; } = "🎨";
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryDescription { get; set; }
    public string Status { get; set; } = "Inactive";
    public bool IsActive { get; set; }
    public int ArtworkCount { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AdminCategoryUpsertRequest
{
    public string? CategoryIcon { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryDescription { get; set; }
    public string? Status { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AdminCategoryStatsDto
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Inactive { get; set; }
    public int UnderReview { get; set; }
    public int TotalArtworks { get; set; }
}
