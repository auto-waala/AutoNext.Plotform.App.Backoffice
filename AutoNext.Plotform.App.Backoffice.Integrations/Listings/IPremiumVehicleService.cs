using AutoNext.Plotform.App.Backoffice.Models.Common;
using AutoNext.Plotform.App.Backoffice.Models.Listings;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Listings
{
    public interface IPremiumVehicleService
    {
        // Get single
        Task<PremiumVehicleResponseDto?> GetByIdAsync(string id);
        Task<PremiumVehicleResponseDto?> GetBySlugAsync(string slug);
        Task<PremiumVehicleResponseDto?> GetByModelSlugAsync(string modelSlug);

        // Get collections
        Task<PagedResult<PremiumVehicleResponseDto>> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null);
        Task<IEnumerable<PremiumVehicleSummaryDto>> GetActivePremiumVehiclesAsync(int limit = 50);
        Task<IEnumerable<PremiumVehicleSummaryDto>> GetTopPriorityPremiumVehiclesAsync(int limit = 10);

        // Filtered collections
        Task<IEnumerable<PremiumVehicleSummaryDto>> GetByBrandAsync(string brandName, int limit = 20);
        Task<IEnumerable<PremiumVehicleSummaryDto>> GetByVehicleTypeAsync(string vehicleType, int limit = 20);
        Task<IEnumerable<PremiumVehicleSummaryDto>> GetByPriceRangeAsync(decimal minPrice, decimal maxPrice, int limit = 20);
        Task<IEnumerable<PremiumVehicleSummaryDto>> GetByCityAsync(string city, int limit = 20);

        // Search
        Task<IEnumerable<PremiumVehicleSummaryDto>> SearchAsync(string searchTerm, int limit = 20);

        // CRUD Operations
        Task<PremiumVehicleResponseDto> CreateAsync(PremiumVehicleRequestDto request);
        Task<PremiumVehicleResponseDto?> UpdateAsync(string id, PremiumVehicleRequestDto request);
        Task<bool> DeleteAsync(string id);

        // Premium-specific operations
        Task<bool> UpdatePriorityAsync(string id, int priority);
        Task<bool> ActivateAsync(string id, DateTime? endDate = null);
        Task<bool> DeactivateAsync(string id);
        Task<bool> BulkUpdatePriorityAsync(Dictionary<string, int> priorityUpdates);

        // Engagement
        Task<bool> IncrementViewsAsync(string id);
        Task<bool> IncrementLikesAsync(string id);
        Task<bool> IncrementSharesAsync(string id);
        Task<bool> IncrementEnquiriesAsync(string id);

        // Rating
        Task<bool> AddRatingAsync(string id, PremiumVehicleUserRatingDto rating);

        // Validation
        Task<bool> IsSlugUniqueAsync(string slug, string? excludeId = null);
        Task<bool> IsModelSlugUniqueAsync(string modelSlug, string? excludeId = null);
    }
}
