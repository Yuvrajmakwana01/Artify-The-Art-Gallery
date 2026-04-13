using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class AdminOrdersViewModel
    {
        public List<AdminOrderRowViewModel> Orders { get; set; } = [];
        public List<string> Categories { get; set; } = [];
        public decimal TotalRevenue { get; set; }
        public int OrderVolume { get; set; }
        public decimal AverageOrderValue { get; set; }
    }
}