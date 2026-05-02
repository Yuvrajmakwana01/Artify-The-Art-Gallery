using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class t_ArtistTransactionLog
    {
        public int      PaymentId      { get; set; }
        public string   ArtworkTitle   { get; set; } = string.Empty;
        public string   BuyerName      { get; set; } = string.Empty;
        public decimal  AmountPaid     { get; set; }
        public decimal  Commission     { get; set; }
        public decimal  NetPayout      { get; set; }
        public string   PaymentStatus  { get; set; } = string.Empty;
        public DateTime? PaidAt        { get; set; }
    }
}