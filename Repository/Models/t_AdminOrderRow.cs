using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class AdminOrderRowViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string BuyerInitials { get; set; } = string.Empty;
        public string BuyerName { get; set; } = string.Empty;
        public string BuyerTone { get; set; } = string.Empty;
        public string ArtworkName { get; set; } = string.Empty;
        public string ArtworkType { get; set; } = string.Empty;
        public string ArtworkTone { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string AmountText { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
    }
}