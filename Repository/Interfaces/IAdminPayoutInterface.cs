using Repository.Models;

namespace Repository.Interfaces
{
    public interface IAdminPayoutInterface
    {
        Task<List<AdminTransactionLogDto>> GetTransactionLogsAsync(CancellationToken cancellationToken = default);
        Task<List<AdminPendingPayoutDto>> GetPendingPayoutsAsync(int? artistId = null, CancellationToken cancellationToken = default);
        Task<List<AdminPayoutHistoryDto>> GetPayoutHistoryAsync(CancellationToken cancellationToken = default);
        Task<List<AdminPayoutArtistFilterDto>> GetPendingPayoutArtistsAsync(CancellationToken cancellationToken = default);
        Task<AdminPayoutAnalyticsDto> GetPayoutAnalyticsAsync(CancellationToken cancellationToken = default);
        Task<bool> ApprovePayoutAsync(int payoutId, CancellationToken cancellationToken = default);
        Task<bool> RejectPayoutAsync(int payoutId, CancellationToken cancellationToken = default);
    }
}
