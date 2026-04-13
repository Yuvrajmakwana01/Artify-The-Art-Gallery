namespace Repository;

public class t_FullDashboardDto
{
    public AdminDashboard Summary { get; set; }
    public List<t_RevenueDto> Revenue { get; set; }
    public List<t_UsersCount> Users { get; set; }
    public t_TotalCount Count { get; set; }
    public List<t_TopArtist> Artists { get; set; }
    public List<t_SellingCategory> Categories { get; set; }
    public List<t_RecentActivity> Activities { get; set; }

}
