using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class vm_Artwork : t_Artwork
    {
        public string CategoryName { get; set; }
        public string CategoryIcon { get; set; } // For the 🎨 emoji
        public string ArtistName { get; set; }
    }
}