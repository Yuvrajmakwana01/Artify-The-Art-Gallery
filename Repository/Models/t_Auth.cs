using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace Repository.Models
{
    public class t_Auth
    {
        
    }

    // ── Admin domain model (maps to t_admin table) ──────────────────────────
    public class t_Admin
    {
        public int c_AdminId { get; set; }

        [Required(ErrorMessage = "Admin Name is required")]
        [StringLength(50)]
        public string c_AdminName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string c_Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(70)]
        public string c_Password { get; set; }
    }

    // ── ViewModel for admin login form ──────────────────────────────────────
    public class vm_AdminLogin
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email address")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        public string c_Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(50, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 50 characters")]
        public string c_Password { get; set; }
    }
}