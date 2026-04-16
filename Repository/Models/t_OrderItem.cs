using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class t_OrderItem
    {
        public int ItemId { get; set; }
        public int OrderId { get; set; }
        public int ArtworkId { get; set; }
        public decimal PriceAtPurchase { get; set; }

         // ── Joined from t_artwork ──────────────────────────────────────────────
        public string  Title             { get; set; } = string.Empty;
        public string? Description       { get; set; }
        public string? PreviewPath       { get; set; }
        public string? CategoryName      { get; set; }   // joined from t_category

        // ── Joined from t_artist_profile ──────────────────────────────────────
        public string  ArtistName        { get; set; } = string.Empty;

    }
}