using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class OrderSummary
    {
        public int      OrderId     { get; set; }
        public decimal  TotalAmount { get; set; }
        public string   OrderStatus { get; set; } = string.Empty;
        public DateTime CreatedAt   { get; set; }
        public int      ItemCount   { get; set; }

        /// <summary>Comma-separated artwork titles (first ~3) for preview text.</summary>
        public string   PreviewTitles  { get; set; } = string.Empty;

        /// <summary>Comma-separated artist names for preview text.</summary>
        public string   PreviewArtists    { get; set; } = string.Empty;

        /// <summary>Preview path of the FIRST artwork in the order – used as thumbnail on the list page.</summary>
        public string?  FirstPreviewPath  { get; set; }
    }
}