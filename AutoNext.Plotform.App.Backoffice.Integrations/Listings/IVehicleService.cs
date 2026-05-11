using AutoNext.Plotform.App.Backoffice.Models.Common;
using AutoNext.Plotform.App.Backoffice.Models.DTO;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Listings
{
    public interface IVehicleService
    {
        // Queries
        Task<VehicleDto?> GetByIdAsync(string id);
        Task<PagedResult<VehicleDto>> GetAllAsync(int page, int pageSize);
        Task<PagedResult<VehicleDto>> SearchAsync(VehicleSearchRequest request);
        Task<PagedResult<VehicleDto>> GetBySellerAsync(string sellerId, int page, int pageSize);

        // Commands
        Task<VehicleDto> CreateAsync(CreateVehicleRequest request, string sellerId);
        Task<VehicleDto?> UpdateAsync(string id, UpdateVehicleRequest request);
        Task<bool> DeleteAsync(string id, string deletedBy = null);   // soft delete
        Task<bool> HardDeleteAsync(string id);                 // permanent
        Task IncrementViewsAsync(string id);
    }
}
