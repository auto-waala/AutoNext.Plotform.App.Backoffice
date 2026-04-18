using AutoNext.Plotform.App.Backoffice.Models.Core;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface ICategoryService
    {
        Task<Category?> GetCategoryByIdAsync(Guid categoryId);
        Task<Category?> GetCategoryByCodeAsync(string code);
        Task<Category?> GetCategoryBySlugAsync(string slug);
        Task<IEnumerable<Category>> GetAllCategoriesAsync();
        Task<IEnumerable<Category>> GetActiveCategoriesAsync();
        Task<IEnumerable<Category>> GetMainCategoriesAsync();
        Task<IEnumerable<Category>> GetSubCategoriesAsync(Guid parentCategoryId);
        Task<Category> CreateCategoryAsync(Category createDto);
        Task<Category?> UpdateCategoryAsync(Guid categoryId, Category updateDto);
        Task<bool> DeleteCategoryAsync(Guid categoryId);
        Task<bool> ToggleCategoryStatusAsync(Guid categoryId, bool isActive);
        Task<bool> ReorderCategoriesAsync(Dictionary<Guid, int> orderMap);
    }
}
