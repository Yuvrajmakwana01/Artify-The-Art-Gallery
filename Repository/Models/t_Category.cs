using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class t_Category
    {
        [Key]
        public int c_category_id { get; set; }

        [Required]
        [StringLength(50)]
        public string c_category_name { get; set; }

        // Mapped to your 'category_status' enum in PG
        public string c_is_active { get; set; } = "Active";

        [StringLength(255)]
        public string? c_category_description { get; set; }

        public DateTime c_created_at { get; set; } = DateTime.Now;

        [StringLength(16)]
        public string? c_icon { get; set; }
    }
}