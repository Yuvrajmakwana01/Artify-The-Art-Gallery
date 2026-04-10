using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repository.Models;

namespace Repository.Interfaces
{
    public interface IAuthInterface
    {
        Task<int> UserRegister(t_UserRegister model);
        Task<t_UserRegister?> UserLogin(t_UserLogin model);

         Task<t_UserRegister?> GetUserByEmail(string email);
        Task<int> UpdatePassword(string email, string newPassword);
    }
}