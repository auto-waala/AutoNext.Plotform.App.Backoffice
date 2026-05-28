using AutoNext.Plotform.App.Backoffice.Models.Common;
using AutoNext.Plotform.App.Backoffice.Models.DTO;


namespace AutoNext.Plotform.App.Backoffice.Integrations.Listings
{
    public interface IUpcomingVehiclesService
    {
        // Get single
        Task<UpcomingVehicleResponseDto?> GetByIdAsync(string id);
        Task<UpcomingVehicleResponseDto?> GetBySlugAsync(string slug);
        Task<UpcomingVehicleResponseDto?> GetByModelSlugAsync(string modelSlug);

        // Get collections
        Task<PagedResult<UpcomingVehicleResponseDto>> GetAllAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null);

        Task<IEnumerable<UpcomingVehicleSummaryDto>> GetActiveUpcomingVehiclesAsync(
            int limit = 50);

        Task<IEnumerable<UpcomingVehicleSummaryDto>> GetFeaturedUpcomingVehiclesAsync(
            int limit = 10);

        Task<IEnumerable<UpcomingVehicleSummaryDto>> GetLatestUpcomingVehiclesAsync(
            int limit = 20);

        // Filtered collections
        Task<IEnumerable<UpcomingVehicleSummaryDto>> GetByBrandAsync(
            string brandName,
            int limit = 20);

        Task<IEnumerable<UpcomingVehicleSummaryDto>> GetByVehicleTypeAsync(
            string vehicleType,
            int limit = 20);

        Task<IEnumerable<UpcomingVehicleSummaryDto>> GetByPriceRangeAsync(
            decimal minPrice,
            decimal maxPrice,
            int limit = 20);

        Task<IEnumerable<UpcomingVehicleSummaryDto>> GetByLaunchPeriodAsync(
            string launchPeriod,
            int limit = 20);

        Task<IEnumerable<UpcomingVehicleSummaryDto>> GetUpcomingLaunchesAsync(
            int days = 30);

        // Search
        Task<IEnumerable<UpcomingVehicleSummaryDto>> SearchAsync(
            string searchTerm,
            int limit = 20);

        // CRUD Operations
        Task<UpcomingVehicleResponseDto> CreateAsync(UpcomingVehicleRequestDto request);
        Task<UpcomingVehicleResponseDto?> UpdateAsync(string id, UpcomingVehicleRequestDto request);
        Task<bool> DeleteAsync(string id);

        // Feature operations
        Task<bool> MarkAsFeaturedAsync(string id);
        Task<bool> RemoveFeaturedAsync(string id);
        Task<bool> UpdatePriorityAsync(string id, int priority);
        Task<bool> MarkAsLaunchedAsync(string id);

        // Status operations
        Task<bool> ActivateAsync(string id, DateTime? launchDate = null);
        Task<bool> DeactivateAsync(string id);

        // Bulk Operations
        Task<bool> BulkUpdatePriorityAsync(Dictionary<string, int> priorityUpdates);
        Task<bool> BulkUpdateFeaturedAsync(Dictionary<string, bool> featuredUpdates);
        Task<bool> BulkActivateAsync(List<string> ids, DateTime? launchDate = null);
        Task<bool> BulkDeactivateAsync(List<string> ids);
        Task<bool> BulkMarkAsLaunchedAsync(List<string> ids);

        // Engagement
        Task<bool> IncrementViewsAsync(string id);
        Task<bool> IncrementLikesAsync(string id);
        Task<bool> IncrementSharesAsync(string id);
        Task<bool> IncrementEnquiriesAsync(string id);

        // Rating
        Task<bool> AddRatingAsync(string id, UpcomingVehicleUserRatingDto rating);

        // Validation
        Task<bool> IsSlugUniqueAsync(string slug, string? excludeId = null);
        Task<bool> IsModelSlugUniqueAsync(string modelSlug, string? excludeId = null);
    }
}
