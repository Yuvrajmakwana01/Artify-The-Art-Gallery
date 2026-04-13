using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repository.Models;

namespace Repository.Interfaces
{
    public interface IAdminOrderInterface
    {

        Task<AdminOrdersViewModel> GetOrdersDashboardAsync(CancellationToken cancellationToken = default);
        Task<AdminAnalyticsViewModel> GetAnalyticsDashboardAsync(string? period = null, CancellationToken cancellationToken = default);
    }
}