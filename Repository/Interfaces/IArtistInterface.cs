using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repository.Models;

namespace Repository.Interfaces
{
    public interface IArtistInterface
    {
        Task<int> Register(t_Artist user);
        Task<t_Artist> Login(vm_Login user);
    }
}