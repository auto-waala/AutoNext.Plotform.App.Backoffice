using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Newtonsoft.Json;
using System.Text;
using System.Net.Http.Json;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public class CategoryService : ICategoryService
    {
        private readonly HttpClient _httpClient;

        public CategoryService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IEnumerable<CategoryResponseDto>> GetAllCategoriesAsync()
        {
            var response = await _httpClient.GetAsync("api/v1/categories");

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<CategoryResponseDto>();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IEnumerable<CategoryResponseDto>>(content)
                   ?? Enumerable.Empty<CategoryResponseDto>();
        }

        public async Task<IEnumerable<CategoryResponseDto>> GetActiveCategoriesAsync()
        {
            var response = await _httpClient.GetAsync("api/v1/categories/active");

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<CategoryResponseDto>();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IEnumerable<CategoryResponseDto>>(content)
                   ?? Enumerable.Empty<CategoryResponseDto>();
        }

        public async Task<IEnumerable<CategoryResponseDto>> GetMainCategoriesAsync()
        {
            var response = await _httpClient.GetAsync("api/v1/categories/main");

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<CategoryResponseDto>();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IEnumerable<CategoryResponseDto>>(content)
                   ?? Enumerable.Empty<CategoryResponseDto>();
        }

        public async Task<IEnumerable<CategoryTreeDto>> GetCategoryTreeAsync()
        {
            var response = await _httpClient.GetAsync("api/v1/categories/tree");

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<CategoryTreeDto>();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IEnumerable<CategoryTreeDto>>(content)
                   ?? Enumerable.Empty<CategoryTreeDto>();
        }

        public async Task<CategoryResponseDto?> GetCategoryByIdAsync(Guid categoryId)
        {
            var response = await _httpClient.GetAsync($"api/v1/categories/{categoryId}");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<CategoryResponseDto>(content);
        }

        public async Task<CategoryResponseDto?> GetCategoryByCodeAsync(string code)
        {
            var response = await _httpClient.GetAsync($"api/v1/categories/code/{code}");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<CategoryResponseDto>(content);
        }

        public async Task<CategoryResponseDto?> GetCategoryBySlugAsync(string slug)
        {
            var response = await _httpClient.GetAsync($"api/v1/categories/slug/{slug}");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<CategoryResponseDto>(content);
        }

        public async Task<IEnumerable<CategoryResponseDto>> GetSubCategoriesAsync(Guid parentCategoryId)
        {
            var response = await _httpClient.GetAsync($"api/v1/categories/{parentCategoryId}/subcategories");

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<CategoryResponseDto>();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IEnumerable<CategoryResponseDto>>(content)
                   ?? Enumerable.Empty<CategoryResponseDto>();
        }

        public async Task<CategoryResponseDto> CreateCategoryAsync(CategoryCreateDto createDto)
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/categories", createDto);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<CategoryResponseDto>(content)!;
        }

        public async Task<CategoryResponseDto?> UpdateCategoryAsync(Guid categoryId, CategoryUpdateDto updateDto)
        {
            var response = await _httpClient.PutAsJsonAsync($"api/v1/categories/{categoryId}", updateDto);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<CategoryResponseDto>(content);
        }

        public async Task<bool> ToggleCategoryStatusAsync(Guid categoryId, bool isActive)
        {
            var response = await _httpClient.PatchAsync(
                $"api/v1/categories/{categoryId}/toggle/{isActive}",
                null
            );

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> ReorderCategoriesAsync(Dictionary<Guid, int> orderMap)
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/categories/reorder", orderMap);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteCategoryAsync(Guid categoryId)
        {
            var response = await _httpClient.DeleteAsync($"api/v1/categories/{categoryId}");
            return response.IsSuccessStatusCode;
        }
    }
}
