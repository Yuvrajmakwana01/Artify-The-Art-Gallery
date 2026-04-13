namespace Repository;

public class t_RevenueDto
{    
    public DateOnly Period { get; set; }  
    public string Label { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal PlatformRevenue { get; set; }
    public decimal ArtistRevenue { get; set; }
}
