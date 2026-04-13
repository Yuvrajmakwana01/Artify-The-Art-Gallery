namespace Repository;

public interface IAdminInterface
{
  Task<AdminDashboard> GetAllDashboardInfo();
  Task<List<t_RevenueDto>> GetRevenue(string type);
  Task<List<t_UsersCount>> GetUsersCount(string type);
  Task<t_TotalCount> GetTotalUsersCount();
  Task<List<t_SellingCategory>> TopSellingCategory();
  Task<List<t_TopArtist>> TopPerformingArtist();
  Task<List<t_RecentActivity>> RecentActivity();
}
