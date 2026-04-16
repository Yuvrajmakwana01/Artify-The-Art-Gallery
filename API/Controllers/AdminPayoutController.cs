using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Interfaces;
using Repository.Models;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminPayoutController : ControllerBase
    {
        private readonly IAdminPayoutInterface _payoutRepository;

        public AdminPayoutController(IAdminPayoutInterface payoutRepository)
        {
            _payoutRepository = payoutRepository;
        }

        [HttpGet("transaction-logs")]
        public async Task<ActionResult<List<AdminTransactionLogDto>>> GetTransactionLogs(CancellationToken cancellationToken)
        {
            var data = await _payoutRepository.GetTransactionLogsAsync(cancellationToken);
            return Ok(data);
        }

        [HttpGet("pending-payouts")]
        public async Task<ActionResult<List<AdminPendingPayoutDto>>> GetPendingPayouts([FromQuery] int? artistId, CancellationToken cancellationToken)
        {
            var data = await _payoutRepository.GetPendingPayoutsAsync(artistId, cancellationToken);
            return Ok(data);
        }

        [HttpGet("payout-history")]
        public async Task<ActionResult<List<AdminPayoutHistoryDto>>> GetPayoutHistory(CancellationToken cancellationToken)
        {
            var data = await _payoutRepository.GetPayoutHistoryAsync(cancellationToken);
            return Ok(data);
        }

        [HttpGet("pending-artists")]
        public async Task<ActionResult<List<AdminPayoutArtistFilterDto>>> GetPendingPayoutArtists(CancellationToken cancellationToken)
        {
            var data = await _payoutRepository.GetPendingPayoutArtistsAsync(cancellationToken);
            return Ok(data);
        }

        [HttpGet("analytics")]
        public async Task<ActionResult<AdminPayoutAnalyticsDto>> GetAnalytics(CancellationToken cancellationToken)
        {
            var data = await _payoutRepository.GetPayoutAnalyticsAsync(cancellationToken);
            return Ok(data);
        }

        [HttpPost("approve/{id:int}")]
        public async Task<IActionResult> ApprovePayout(int id, CancellationToken cancellationToken)
        {
            var ok = await _payoutRepository.ApprovePayoutAsync(id, cancellationToken);
            if (!ok) return NotFound(new { message = "Pending payout not found." });

            return Ok(new { message = "Payout approved and transferred successfully." });
        }

        [HttpPost("reject/{id:int}")]
        public async Task<IActionResult> RejectPayout(int id, CancellationToken cancellationToken)
        {
            var ok = await _payoutRepository.RejectPayoutAsync(id, cancellationToken);
            if (!ok) return NotFound(new { message = "Pending payout not found." });

            return Ok(new { message = "Payout rejected successfully." });
        }
    }
}
