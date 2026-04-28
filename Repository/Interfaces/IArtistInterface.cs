using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repository.Models;

namespace Repository.Interfaces
{
    public interface IArtistInterface
    {
    
        Task<t_ArtistProfile> GetArtistById(int artistId);

        Task<int> EditArtistProfile(t_ArtistProfile model);
        Task<int> Register(t_Artist user);
        Task<t_Artist> Login(vm_Login user);

        Task<t_Artist_Dashboard> GetDashboardData(int artistId);


        Task<List<object>> GetMonthlyRevenue(int artistId);

        Task<List<object>> GetSalesByCategory(int artistId);


        Task<int> ChangePassword(int artistId, string oldPwd, string newPwd);

        /// <summary>Sets c_is_active = false for the given artist (soft-deactivation).</summary>
        Task<int> DeactivateAccount(int artistId);

        public Task<t_Artist_EarningsSummary> GetEarningsSummary(int artistId);
    }
}