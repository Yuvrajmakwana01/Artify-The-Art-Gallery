using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class t_Wishlist
    {
         public int WishlistId { get; set; }
        public int BuyerId { get; set; }
        public int ArtworkId { get; set; }

        // Joined fields (populated by JOIN queries)
        public string ArtworkTitle { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? PreviewPath { get; set; }
        public string? CategoryName { get; set; }
        public bool IsAvailable { get; set; } = true;
    }
}
