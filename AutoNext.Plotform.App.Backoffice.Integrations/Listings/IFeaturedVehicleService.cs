using AutoNext.Plotform.App.Backoffice.Models.Common;
using AutoNext.Plotform.App.Backoffice.Models.DTO;


namespace AutoNext.Plotform.App.Backoffice.Integrations.Listings
{
    public interface IFeaturedVehicleService
    {
        // Get single
        Task<FeaturedVehicleResponseDto?> GetByIdAsync(string id);
        Task<FeaturedVehicleResponseDto?> GetBySlugAsync(string slug);
        Task<FeaturedVehicleResponseDto?> GetByModelSlugAsync(string modelSlug);

        // Get collections
        Task<PagedResult<FeaturedVehicleResponseDto>> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null);

        // Filtered collections
        Task<IEnumerable<FeaturedVehicleSummaryDto>> GetActiveFeaturedVehiclesAsync(int limit = 50);
        Task<IEnumerable<FeaturedVehicleSummaryDto>> GetTopPriorityFeaturedVehiclesAsync(int limit = 10);
        Task<IEnumerable<FeaturedVehicleSummaryDto>> GetByBrandAsync(string brandName, int limit = 20);
        Task<IEnumerable<FeaturedVehicleSummaryDto>> GetByVehicleTypeAsync(string vehicleType, int limit = 20);
        Task<IEnumerable<FeaturedVehicleSummaryDto>> GetByPriceRangeAsync(decimal minPrice, decimal maxPrice, int limit = 20);
        Task<IEnumerable<FeaturedVehicleSummaryDto>> GetByCityAsync(string city, int limit = 20);
        Task<IEnumerable<FeaturedVehicleSummaryDto>> SearchAsync(string searchTerm, int limit = 20);

        // CRUD Operations
        Task<FeaturedVehicleResponseDto> CreateAsync(FeaturedVehicleRequestDto request);
        Task<FeaturedVehicleResponseDto?> UpdateAsync(string id, FeaturedVehicleRequestDto request);
        Task<bool> DeleteAsync(string id);

        // Featured-specific operations
        Task<bool> UpdatePriorityAsync(string id, int priority);
        Task<bool> BulkUpdatePriorityAsync(Dictionary<string, int> priorityUpdates);
        Task<bool> ActivateAsync(string id, DateTime? endDate = null);
        Task<bool> DeactivateAsync(string id);

        // Engagement operations
        Task<bool> IncrementViewsAsync(string id);
        Task<bool> IncrementLikesAsync(string id);
        Task<bool> IncrementSharesAsync(string id);
        Task<bool> IncrementEnquiriesAsync(string id);

        // Rating operations
        Task<bool> AddRatingAsync(string id, UserRatingDto rating);
    }
}
