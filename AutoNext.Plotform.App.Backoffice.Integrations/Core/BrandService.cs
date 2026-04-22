using Newtonsoft.Json;
using System.Net.Http.Json;
using AutoNext.Plotform.App.Backoffice.Models.DTO;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{

    public class BrandService : IBrandService
    {
        private readonly HttpClient _httpClient;

        public BrandService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IEnumerable<BrandResponseDto>> GetAllBrandsAsync()
        {
            var response = await _httpClient.GetAsync("api/v1/brands");

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<BrandResponseDto>();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IEnumerable<BrandResponseDto>>(content) ?? Enumerable.Empty<BrandResponseDto>();
        }

        public async Task<IEnumerable<BrandResponseDto>> GetActiveBrandsAsync()
        {
            var response = await _httpClient.GetAsync("api/v1/brands/active");

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<BrandResponseDto>();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IEnumerable<BrandResponseDto>>(content) ?? Enumerable.Empty<BrandResponseDto>();
        }

        public async Task<BrandResponseDto?> GetBrandByIdAsync(Guid brandId)
        {
            var response = await _httpClient.GetAsync($"api/v1/brands/{brandId}");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<BrandResponseDto>(content);
        }

        public async Task<IEnumerable<BrandResponseDto>> GetBrandsByCategoryAsync(string categoryCode)
        {
            var response = await _httpClient.GetAsync($"api/v1/brands/category/{categoryCode}");

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<BrandResponseDto>();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IEnumerable<BrandResponseDto>>(content) ?? Enumerable.Empty<BrandResponseDto>();
        }

        public async Task<IEnumerable<BrandResponseDto>> GetBrandsByCountryAsync(string countryCode)
        {
            var response = await _httpClient.GetAsync($"api/v1/brands/country/{countryCode}");

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<BrandResponseDto>();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IEnumerable<BrandResponseDto>>(content) ?? Enumerable.Empty<BrandResponseDto>();
        }

        public async Task<BrandResponseDto> CreateBrandAsync(BrandCreateDto createDto)
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/brands", createDto);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<BrandResponseDto>(content)!;
        }

        public async Task<BrandResponseDto?> UpdateBrandAsync(Guid brandId, BrandUpdateDto updateDto)
        {
            var response = await _httpClient.PutAsJsonAsync($"api/v1/brands/{brandId}", updateDto);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<BrandResponseDto>(content);
        }

        public async Task<bool> ToggleBrandStatusAsync(Guid brandId, bool isActive)
        {
            var response = await _httpClient.PatchAsync($"api/v1/brands/{brandId}/toggle/{isActive}", null);

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteBrandAsync(Guid brandId)
        {
            var response = await _httpClient.DeleteAsync($"api/v1/brands/{brandId}");
            return response.IsSuccessStatusCode;
        }
    }
}
