using System.ComponentModel.DataAnnotations;

namespace Repository.Models
{
    public class t_ForgotPassword
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string c_Email { get; set; }
    }

    public class t_ResetPassword
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string c_Email { get; set; }

        [Required(ErrorMessage = "OTP is required")]
        public string c_Otp { get; set; }

        [Required(ErrorMessage = "New password is required")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[@$!%*?&]).{6,}$",
            ErrorMessage = "Password must contain uppercase, lowercase, number and special character")]
        public string c_NewPassword { get; set; }

        [Required(ErrorMessage = "Confirm password is required")]
        [Compare("c_NewPassword", ErrorMessage = "Passwords do not match")]
        public string c_ConfirmPassword { get; set; }
    }
}
