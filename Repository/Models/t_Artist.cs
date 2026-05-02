using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Repository.Models
{
    public class t_Artist
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int c_User_Id { get; set; }

        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Full Name")]
        public string c_Full_Name { get; set; }

        [Required(ErrorMessage = "Username is required")]
        public string c_UserName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string c_Email { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        public string c_Gender { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{6,}$", ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character")]
        public string c_Password { get; set; }

        public string? c_Mobile { get; set; }
        public string? c_Profile_Image { get; set; }

        public string? c_BioGraphy { get; set; }


        public string? c_Cover_Image { get; set; }


        public string ? c_Social_Media_Link { get; set; }
        

        public bool c_Is_Active { get; set; } = false;

        public bool c_Is_Blocked { get; set; } = false;

        public IFormFile? ProfilePicture { get; set; }

        public IFormFile? CoverPicture { get; set; }
    }
}