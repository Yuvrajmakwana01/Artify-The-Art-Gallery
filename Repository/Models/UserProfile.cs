using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class UserProfile
    {
        public int c_UserId { get; set; }
        public string? c_UserName { get; set; }
        public string? c_Email { get; set; }
        public string? c_FullName { get; set; }
        public string? c_Mobile { get; set; }
        public string? c_Address { get; set; }
        public string? c_Gender { get; set; }
        public string? c_Image { get; set; }
        public string? c_PreferredStyles { get; set; }
        public DateTime? c_CreatedAt { get; set; }
    }
}