using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repository.Models;

namespace Repository.Interfaces
{
    public interface IArtistInterface
    {
        public Task<t_Artist_Dashboard> GetDashboardData(int artistId);
    }
}