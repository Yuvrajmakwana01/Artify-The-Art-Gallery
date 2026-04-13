using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repository.Models;

namespace Repository.Interfaces
{
    public interface IAuthInterface
    {
        // ── Admin auth ──────────────────────────────────────────────────────
        Task<t_Admin?> AdminLogin(vm_AdminLogin admin);
    }
}