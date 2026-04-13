using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Repository.Models
{
    public class t_Artist_Dashboard
    {
        public int c_ArtisrtId {get; set;}

        public string c_ArtistName {get; set;}

        public string c_Email {get; set;}

        public string c_Biography {get; set;}

        public string c_CoverImageName {get;set;}

        public IFormFile c_CoverImage {get; set;}

        public string c_ProfileImageName {get; set;}

        public string c_ProfileImage {get; set;}

        public decimal c_RatingAvg {get; set;}

        public int c_TotalArtworkCount {get; set;}

        public int c_TotalLikeCount {get; set;}

        public int c_TotalSellCount {get; set;}


    }
}