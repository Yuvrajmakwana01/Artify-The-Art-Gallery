using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    // ─────────────────────────────────────────────
    //  Core domain model (maps to t_artwork join)
    // ─────────────────────────────────────────────
    public class ArtworkModel
    {
        public int c_ArtworkId { get; set; }
        public int c_ArtistId { get; set; }
        public string c_ArtistName { get; set; } = string.Empty;
        public int c_CategoryId { get; set; }
        public string c_CategoryName { get; set; } = string.Empty;
        public string c_Title { get; set; } = string.Empty;
        public string c_Description { get; set; } = string.Empty;
        public decimal c_Price { get; set; }
        public string c_PreviewPath { get; set; } = string.Empty;
        public string c_ApprovalStatus { get; set; } = "Pending";
        public string c_AdminNote { get; set; } = string.Empty;
        public DateTime c_CreatedAt { get; set; }
        public int c_LikesCount { get; set; }
    }

    // ─────────────────────────────────────────────
    //  Request DTOs
    // ─────────────────────────────────────────────
    public class ApproveArtworkRequest
    {
        public string? c_AdminNote { get; set; }
    }

    public class RejectArtworkRequest
    {
        public string c_AdminNote { get; set; } = string.Empty;   // required on reject
    }

    // ─────────────────────────────────────────────
    //  Elasticsearch document
    // ─────────────────────────────────────────────
    public class ArtworkElasticDoc
    {
        public int c_ArtworkId { get; set; }
        public string c_Title { get; set; } = string.Empty;
        public string c_Description { get; set; } = string.Empty;
        public int c_ArtistId { get; set; }
        public string c_ArtistName { get; set; } = string.Empty;
        public int c_CategoryId { get; set; }
        public string c_CategoryName { get; set; } = string.Empty;
        public decimal c_Price { get; set; }
        public string c_Status { get; set; } = "Approved";
        public DateTime c_CreatedAt { get; set; }
        public int c_Likes { get; set; }
    }

    // ─────────────────────────────────────────────
    // ─────────────────────────────────────────────
    // ─────────────────────────────────────────────
    //  Paginated API response wrapper
    // ─────────────────────────────────────────────
    public class PagedResult<T>
    {
        public List<T> c_Data { get; set; } = new();
        public int c_Total { get; set; }
        public int c_Page { get; set; }
        public int c_PageSize { get; set; }
    }
}