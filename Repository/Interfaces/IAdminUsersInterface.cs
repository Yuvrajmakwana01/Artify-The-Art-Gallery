using Repository.Models;

namespace Repository.Interfaces;

public interface IAdminUsersInterface
{
    Task<List<AdminUserDto>> GetUsersAsync(string? search);
    Task<AdminUserStatsDto> GetUserStatsAsync();
    Task<AdminUserDto?> GetUserByIdAsync(int userId);
    Task<bool> UpdateUserAsync(int userId, AdminUserUpdateRequest request);
    Task<bool> DeleteUserAsync(int userId);
}
