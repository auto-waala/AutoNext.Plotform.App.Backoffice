using AutoNext.Plotform.App.Backoffice.Models.DTO;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface ICategoryService
    {
        Task<CategoryResponseDto?> GetCategoryByIdAsync(Guid categoryId);
        Task<CategoryResponseDto?> GetCategoryByCodeAsync(string code);
        Task<CategoryResponseDto?> GetCategoryBySlugAsync(string slug);
        Task<IEnumerable<CategoryResponseDto>> GetAllCategoriesAsync();
        Task<IEnumerable<CategoryResponseDto>> GetActiveCategoriesAsync();
        Task<IEnumerable<CategoryResponseDto>> GetMainCategoriesAsync();
        Task<IEnumerable<CategoryResponseDto>> GetSubCategoriesAsync(Guid parentCategoryId);
        Task<IEnumerable<CategoryTreeDto>> GetCategoryTreeAsync();
        Task<CategoryResponseDto> CreateCategoryAsync(CategoryCreateDto createDto);
        Task<CategoryResponseDto?> UpdateCategoryAsync(Guid categoryId, CategoryUpdateDto updateDto);
        Task<bool> DeleteCategoryAsync(Guid categoryId);
        Task<bool> ToggleCategoryStatusAsync(Guid categoryId, bool isActive);
        Task<bool> ReorderCategoriesAsync(Dictionary<Guid, int> orderMap);
    }
}
