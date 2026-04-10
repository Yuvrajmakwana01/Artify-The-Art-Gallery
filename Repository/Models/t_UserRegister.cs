 using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Models
{
    public class t_UserRegister
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int c_UserId { get; set; }

        // Full Name with strong validation
        [Required(ErrorMessage = "Full Name is required")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Full Name must be between 3 and 100 characters")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Full Name can contain only letters and spaces")]
        public string c_FullName { get; set; }

        // Username
        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3)]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can contain only letters, numbers, and underscore")]
        public string c_UserName { get; set; }

        //  Email
        [Required(ErrorMessage = "Email is required")]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email format")]
        [StringLength(100)]
        public string c_Email { get; set; }

        // Password (will be stored as HASH)
        [NotMapped] //  Not stored directly
        [RegularExpression(@"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[@$!%*?&]).{6,}$",ErrorMessage = "Password must contain uppercase, lowercase, number and special character")]
        public string c_Password { get; set; }

        // Password Hash (stored in DB)
        public string? c_PasswordHash { get; set; }

        // Confirm Password (UI only)
        [NotMapped]
        [Required(ErrorMessage = "Confirm Password is required")]
        [Compare("c_Password", ErrorMessage = "Passwords do not match")]
        public string c_ConfirmPassword { get; set; }

        //  Gender
         [Required(ErrorMessage = "Gender is required")]
        [RegularExpression(@"^(Male|Female|Other)$", ErrorMessage = "Invalid gender")]
        public string c_Gender { get; set; }

        //  Mobile
        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Mobile number must be 10 digits")]
        public string? c_Mobile { get; set; }

        // Profile Image
        public string? c_ProfileImage { get; set; }

        // Created Date
        [NotMapped]
        public DateTime c_CreatedAt { get; set; } = DateTime.UtcNow;
    }
}