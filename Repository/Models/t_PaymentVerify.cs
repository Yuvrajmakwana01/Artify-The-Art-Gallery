using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repository.Models
{
    public class t_PaymentVerify
    {
        public string PaypalOrderId { get; set; }

    public string? CaptureId { get; set; } 

    public string Currency { get; set; }

    public List<t_CartItem> Cart { get; set; }
    }

    public class PaymentVerifyWrapper
    {
        public t_PaymentVerify PaymentDetails { get; set; }
        public t_UserRegister UserDetails { get; set; }
    }
}
