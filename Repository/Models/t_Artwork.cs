using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Repository.Models
{
    public class t_Artwork
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int c_artwork_id { get; set; }
        public int c_artist_id { get; set; }
        public int c_category_id { get; set; }
        public string c_title { get; set; }
        public string c_description { get; set; }
        public decimal c_price { get; set; }
        public string? c_preview_path { get; set; }
        public string? c_original_path { get; set; }
        public string c_approval_status { get; set; }
        public string? c_admin_note { get; set; }
        public int c_likes_count { get; set; }
        public int c_sell_count { get; set; }


        public IFormFile ArtworkFile { get; set; }
    }
}