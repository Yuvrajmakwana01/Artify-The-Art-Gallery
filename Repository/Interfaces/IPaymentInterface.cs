using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repository.Models;

namespace Repository.Interfaces
{
    public interface IPaymentInterface
    {
      Task<int> ProcessFullPaymentAsync(int buyerId, t_PaymentVerify model);
    } 
}