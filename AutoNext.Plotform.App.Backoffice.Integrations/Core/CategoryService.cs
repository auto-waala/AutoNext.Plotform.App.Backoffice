using AutoNext.Plotform.App.Backoffice.Models.Core;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public class CategoryService : ICategoryService
    {
        public Task<Category> CreateCategoryAsync(Category createDto)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteCategoryAsync(Guid categoryId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Category>> GetActiveCategoriesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Category>> GetAllCategoriesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Category?> GetCategoryByCodeAsync(string code)
        {
            throw new NotImplementedException();
        }

        public Task<Category?> GetCategoryByIdAsync(Guid categoryId)
        {
            throw new NotImplementedException();
        }

        public Task<Category?> GetCategoryBySlugAsync(string slug)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Category>> GetMainCategoriesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Category>> GetSubCategoriesAsync(Guid parentCategoryId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ReorderCategoriesAsync(Dictionary<Guid, int> orderMap)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ToggleCategoryStatusAsync(Guid categoryId, bool isActive)
        {
            throw new NotImplementedException();
        }

        public Task<Category?> UpdateCategoryAsync(Guid categoryId, Category updateDto)
        {
            throw new NotImplementedException();
        }
    }
}
