using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class t_UserLogin
    {
         // Email  
        [Required(ErrorMessage = "Email is required")]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$",ErrorMessage = "Invalid email format")]
        [StringLength(100)]
        public string c_Email { get; set; }
 
        [Required(ErrorMessage = "Password is required")]
        // [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{6,}$",ErrorMessage = "Password must contain uppercase, lowercase, number and special character")]
        [RegularExpression(@"^(?=.*[a-zA-Z])(?=.*[!@#$%^&*(),.?Short"":{}|<>]).{6,}$", ErrorMessage = "Password must be at least 6 characters long and include one letter and one special character.")]
        public string c_Password { get; set; }

        // [NotMapped] // DB mein nahi jaata
        // [Required(ErrorMessage = "Please complete the captcha")]
        // public string c_CaptchaToken { get; set; }
    }
}