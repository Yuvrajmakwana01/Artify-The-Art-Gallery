using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;
using Repository.Models;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminAnalyticsController : ControllerBase
    {

        private readonly IAdminOrderInterface _adminOrdersRepository;

        public AdminAnalyticsController(IAdminOrderInterface adminOrdersRepository)
        {
            _adminOrdersRepository = adminOrdersRepository;
        }
        [HttpGet("analytics-dashboard")]
        public async Task<ActionResult<AdminAnalyticsViewModel>> GetAnalyticsDashboard([FromQuery] string? period, CancellationToken cancellationToken)
        {
            var model = await _adminOrdersRepository.GetAnalyticsDashboardAsync(period, cancellationToken);
            return Ok(model);
        }
    }
}