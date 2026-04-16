using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class t_Payment
    {
        public int PaymentId { get; set; }
        public int OrderId { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;

        public decimal AmountPaid { get; set; }
        public decimal CommissionDeducted { get; set; }
        public decimal ArtistPayoutAmount { get; set; }

        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime PaidAt { get; set; }
        public string Currency { get; set; } = string.Empty;
    }
}
