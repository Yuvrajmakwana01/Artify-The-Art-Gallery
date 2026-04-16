using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class t_ChangePassword
    {
        [Required]
        public int c_user_id { get; set; }

        [Required(ErrorMessage = "Current password is required")]
        public string c_CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "New password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string c_NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm password is required")]
        [Compare("c_NewPassword", ErrorMessage = "New password and confirm password do not match")]
        public string c_ConfirmPassword { get; set; } = string.Empty;
    }
}