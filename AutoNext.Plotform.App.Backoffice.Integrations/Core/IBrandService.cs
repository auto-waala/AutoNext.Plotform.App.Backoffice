using AutoNext.Plotform.App.Backoffice.Models.Core;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface IBrandService
    {
        Task<Brand> GetBrandByIdAsync(Guid brandId);
        Task<IEnumerable<Brand>> GetAllBrandsAsync();
        Task<IEnumerable<Brand>> GetActiveBrandsAsync();
        Task<IEnumerable<Brand>> GetBrandsByCategoryAsync(string categoryCode);
        Task<IEnumerable<Brand>> GetBrandsByCountryAsync(string countryCode);
        Task<Brand> CreateBrandAsync(Brand createDto);
        Task<Brand?> UpdateBrandAsync(Guid brandId, Brand updateDto);
        Task<bool> DeleteBrandAsync(Guid brandId);
        Task<bool> ToggleBrandStatusAsync(Guid brandId, bool isActive);
    }
}
