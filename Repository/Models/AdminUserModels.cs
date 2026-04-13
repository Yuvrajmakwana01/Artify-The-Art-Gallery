namespace Repository.Models;

public class AdminUserDto
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string? ProfileImage { get; set; }
    public DateTime CreatedAt { get; set; }
    public int OrdersCount { get; set; }
    public decimal TotalSpend { get; set; }
    public string Role { get; set; } = "User";
}

public class AdminUserUpdateRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string? ProfileImage { get; set; }
}

public class AdminUserStatsDto
{
    public int TotalUsers { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSpend { get; set; }
}
