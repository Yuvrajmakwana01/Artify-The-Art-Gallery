using Repository.Models;

namespace Repository.Interfaces;

public interface IAdmincategoiresInteface
{
    Task<List<AdminCategoryDto>> GetCategoriesAsync(string? search, string? status);
    Task<AdminCategoryStatsDto> GetCategoryStatsAsync();
    Task<AdminCategoryDto?> GetCategoryByIdAsync(int categoryId);
    Task<int> AddCategoryAsync(AdminCategoryUpsertRequest request);
    Task<bool> UpdateCategoryAsync(int categoryId, AdminCategoryUpsertRequest request);
    Task<bool> DeleteCategoryAsync(int categoryId);
}
