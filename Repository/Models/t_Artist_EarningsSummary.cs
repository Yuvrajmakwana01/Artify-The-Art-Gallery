using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class t_Artist_EarningsSummary
    {
        public decimal AvailableBalance { get; set; }
        public decimal RequestedBalance { get; set; }
        public decimal RevenueBalance { get; set; }
    }
}