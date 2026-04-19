using AutoNext.Plotform.App.Backoffice.Models.Core;
using System.Net.Http.Json;


namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public class BrandService : IBrandService
    {
        private readonly HttpClient _httpClient;
        public BrandService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        public async Task<Brand> CreateBrandAsync(Brand createDto)
        {
            return new Brand();
        }

        public Task<bool> DeleteBrandAsync(Guid brandId)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<Brand>> GetActiveBrandsAsync()
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<Brand>>("/brands/active");
        }

        public async Task<IEnumerable<Brand>> GetAllBrandsAsync()
        {
            var response = await _httpClient.GetAsync("api/v1/brands");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var brands = System.Text.Json.JsonSerializer.Deserialize<IEnumerable<Brand>>(content);
                return brands ?? Enumerable.Empty<Brand>();
            }
            return Enumerable.Empty<Brand>();
        }

        public Task<Brand> GetBrandByIdAsync(Guid brandId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Brand>> GetBrandsByCategoryAsync(string categoryCode)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Brand>> GetBrandsByCountryAsync(string countryCode)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ToggleBrandStatusAsync(Guid brandId, bool isActive)
        {
            throw new NotImplementedException();
        }

        public Task<Brand?> UpdateBrandAsync(Guid brandId, Brand updateDto)
        {
            throw new NotImplementedException();
        }
    }
}
