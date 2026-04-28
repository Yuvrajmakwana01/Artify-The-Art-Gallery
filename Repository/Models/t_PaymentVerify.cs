using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class t_PaymentVerify
    {
        public string? PaypalOrderId { get; set; }      // nullable — not present on create-order call

        public string? CaptureId { get; set; }

        public string? Currency { get; set; }           // nullable — safe fallback to "USD"

        public decimal Subtotal { get; set; }

        public decimal CommissionAmount { get; set; }

        public decimal TotalAmount { get; set; }

        public List<t_CartItem> Cart { get; set; } = new();
    }

    public class PaymentVerifyWrapper
    {
        public t_PaymentVerify PaymentDetails { get; set; }
        public t_UserRegister UserDetails { get; set; }
    }
}
