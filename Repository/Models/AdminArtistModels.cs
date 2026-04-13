namespace Repository.Models;

public class AdminArtistDto
{
    public int ArtistId { get; set; }
    public string ArtistName { get; set; } = string.Empty;
    public string ArtistEmail { get; set; } = string.Empty;
    public string? Biography { get; set; }
    public string? CoverImage { get; set; }
    public decimal RatingAvg { get; set; }
    public bool IsVerified { get; set; }
    public int RejectedCount { get; set; }
    public int ArtworksCount { get; set; }
    public decimal TotalSales { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "Under Review";
}

public class AdminArtistUpsertRequest
{
    public string ArtistName { get; set; } = string.Empty;
    public string ArtistEmail { get; set; } = string.Empty;
    public string? Biography { get; set; }
    public string? CoverImage { get; set; }
    public string Status { get; set; } = "Under Review";

    public string? FullName { get; set; }
    public string? Username { get; set; }
    public string Gender { get; set; } = "Unknown";
    public string? Mobile { get; set; }
    public string? ProfileImage { get; set; }
    public string UserPasswordHash { get; set; } = "TEMP_HASH_CHANGE_ME";
    public string ArtistPassword { get; set; } = "TEMP_PASSWORD";
}

public class AdminArtistStatsDto
{
    public int TotalArtists { get; set; }
    public int Active { get; set; }
    public int Inactive { get; set; }
    public int UnderReview { get; set; }
    public decimal TotalSales { get; set; }
}
