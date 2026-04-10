using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class vm_Login
    {
        [StringLength(100)]
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string c_Email { get; set; }


        [StringLength(100)]
        [Required(ErrorMessage = "Password is required")]
        public string c_Password { get; set; }
    }
}