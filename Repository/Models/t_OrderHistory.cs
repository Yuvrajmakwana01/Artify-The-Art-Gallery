using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class t_OrderHistory
    {
        public t_Order           Order { get; set; } = new();
        public List<t_OrderItem> Items { get; set; } = new();
    }
}