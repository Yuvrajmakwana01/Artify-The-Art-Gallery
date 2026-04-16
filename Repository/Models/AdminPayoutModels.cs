namespace Repository.Models;

public class AdminTransactionLogDto
{
    public int PaymentId { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
}

public class AdminPendingPayoutDto
{
    public int Id { get; set; }
    public int ArtistId { get; set; }
    public string ArtistName { get; set; } = string.Empty;
    public string RequestMonth { get; set; } = string.Empty;
    public string Artwork { get; set; } = string.Empty;
    public decimal GrossAmount { get; set; }
    public decimal Commission { get; set; }
    public decimal NetAmount { get; set; }
    public int OrdersCount { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime? RequestedAt { get; set; }
}

public class AdminPayoutHistoryDto
{
    public int Id { get; set; }
    public string ArtistName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ProcessedDate { get; set; }
}

public class AdminPayoutAnalyticsDto
{
    public decimal TotalRevenue { get; set; }
    public int SuccessfulTransactions { get; set; }
    public decimal PendingPayoutAmount { get; set; }
    public int PendingPayoutCount { get; set; }
    public decimal FailedOrRefundedAmount { get; set; }
    public List<AdminPayoutAnalyticsPointDto> MonthlyNetPayoutSeries { get; set; } = [];
}

public class AdminPayoutAnalyticsPointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class AdminPayoutArtistFilterDto
{
    public int ArtistId { get; set; }
    public string ArtistName { get; set; } = string.Empty;
}
