// ────────────────────────────────────────────────────────────────────────
// Repository/Models/t_OrderDetail.cs
// ────────────────────────────────────────────────────────────────────────
namespace Repository.Models;

public class t_OrderDetail
{
    public int     OrderId             { get; set; }

    public DateTime OrderDate          { get; set; }
    public string  OrderStatus         { get; set; } = string.Empty;
    public decimal TotalAmount         { get; set; }
    public string  PaymentMethod       { get; set; } = string.Empty;
    public string? TransactionId       { get; set; }
    public string  PaymentStatus       { get; set; } = string.Empty;
    public decimal CommissionDeducted  { get; set; }
    public decimal ArtistPayout        { get; set; }
    public string  BuyerName           { get; set; } = string.Empty;
    public string  BuyerEmail          { get; set; } = string.Empty;
    public string? BuyerPhone          { get; set; }

    //  public t_Order Order { get; set; } = new();

    public List<t_OrderItemDetail> Items { get; set; } = new();
}

public class t_OrderItemDetail
{
    public int     ArtworkId      { get; set; }
    public string  Title          { get; set; } = string.Empty;
    public string  ArtistName     { get; set; } = string.Empty;
    public decimal Price          { get; set; }
    public string? PreviewPath    { get; set; }
    public string? OriginalPath   { get; set; }

    // ── Download tracking via t_download_log ──────────────────────────
    public int  DownloadCount  { get; set; }   // rows in t_download_log
    public int  DownloadsLeft  { get; set; }   // 5 - DownloadCount
    public bool CanDownload    { get; set; }   // DownloadCount < 5
}
