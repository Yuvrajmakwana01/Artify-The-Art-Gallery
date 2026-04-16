using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;
using Repository.Models;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminOrderController : ControllerBase
    {
        private readonly IAdminOrderInterface _adminOrdersRepository;

        public AdminOrderController(IAdminOrderInterface adminOrdersRepository)
        {
            _adminOrdersRepository = adminOrdersRepository;
        }

        [HttpGet("orders-dashboard")]
        public async Task<ActionResult<AdminOrdersViewModel>> GetOrdersDashboard(CancellationToken cancellationToken)
        {
            var model = await _adminOrdersRepository.GetOrdersDashboardAsync(cancellationToken);
            return Ok(model);
        }
    }
}