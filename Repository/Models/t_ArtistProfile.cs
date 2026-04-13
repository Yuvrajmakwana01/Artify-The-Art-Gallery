using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Repository.Models
{
    using Microsoft.AspNetCore.Http;

public class t_ArtistProfile
{
    public int ArtistId { get; set; }

    public string? ArtistName { get; set; }

    public string? Biography { get; set; }

    public bool? IsActive { get; set; }   // ✅ MUST be nullable

    public string? CoverImage { get; set; }

    public IFormFile? CoverImageFile { get; set; }

    public string[]? Urls { get; set; }   // ✅ ARRAY

    // extra fields (optional)
    public string? ArtistEmail { get; set; }
    public decimal RatingAvg { get; set; }
    public bool IsVerified { get; set; }
}
    }
