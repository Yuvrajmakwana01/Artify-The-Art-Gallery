using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repository.Models;

namespace Repository.Interfaces
{
    public interface IUserProfileInterface
    {
        Task<UserProfile?> GetProfileById(int userId);
        Task<int> UpdateProfile(UserProfile profile);
        Task<int> ChangePassword(int artistId, string oldPwd, string newPwd);

    }
}