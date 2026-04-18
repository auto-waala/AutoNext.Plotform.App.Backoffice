using AutoNext.Plotform.App.Backoffice.Models.Core;


namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    internal class BrandService : IBrandService
    {
        public Task<Brand> CreateBrandAsync(Brand createDto)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteBrandAsync(Guid brandId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Brand>> GetActiveBrandsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Brand>> GetAllBrandsAsync()
        {
            throw new NotImplementedException();
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
