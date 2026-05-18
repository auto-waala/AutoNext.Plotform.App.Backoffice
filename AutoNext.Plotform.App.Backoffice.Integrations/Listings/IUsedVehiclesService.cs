using AutoNext.Plotform.App.Backoffice.Models.Common;
using AutoNext.Plotform.App.Backoffice.Models.DTO;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Listings
{
    public interface IUsedVehiclesService
    {
        // Get single
        Task<UsedVehiclesResponseDto?> GetByIdAsync(string id);
        Task<UsedVehiclesResponseDto?> GetBySlugAsync(string slug);
        Task<UsedVehiclesResponseDto?> GetByModelSlugAsync(string modelSlug);

        // Get collections
        Task<PagedResult<UsedVehiclesResponseDto>> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null);
        Task<IEnumerable<UsedVehiclesResponseDto>> GetActiveUsedVehiclesAsync(int limit = 50);
        Task<IEnumerable<UsedVehiclesResponseDto>> GetTopPriorityUsedVehiclesAsync(int limit = 10);
        Task<IEnumerable<UsedVehiclesResponseDto>> GetRecentlyPostedAsync(int days = 7, int limit = 20);

        // Filtered collections
        Task<IEnumerable<UsedVehiclesResponseDto>> GetByBrandAsync(string brandName, int limit = 20);
        Task<IEnumerable<UsedVehiclesResponseDto>> GetByVehicleTypeAsync(string vehicleType, int limit = 20);
        Task<IEnumerable<UsedVehiclesResponseDto>> GetByPriceRangeAsync(decimal minPrice, decimal maxPrice, int limit = 20);
        Task<IEnumerable<UsedVehiclesResponseDto>> GetByCityAsync(string city, int limit = 20);
        Task<IEnumerable<UsedVehiclesResponseDto>> GetByFuelTypeAsync(string fuelType, int limit = 20);
        Task<IEnumerable<UsedVehiclesResponseDto>> GetByTransmissionAsync(string transmission, int limit = 20);
        Task<IEnumerable<UsedVehiclesResponseDto>> GetByYearRangeAsync(int minYear, int maxYear, int limit = 20);
        Task<IEnumerable<UsedVehiclesResponseDto>> GetBySellerTypeAsync(string sellerType, int limit = 20);
        Task<IEnumerable<UsedVehiclesResponseDto>> GetBySellerAsync(string sellerId, int limit = 20);

        // Search
        Task<IEnumerable<UsedVehiclesResponseDto>> SearchAsync(string searchTerm, int limit = 20);
        Task<PagedResult<UsedVehiclesResponseDto>> AdvancedSearchAsync(UsedVehiclesSearchCriteria criteria);

        // CRUD Operations
        Task<UsedVehiclesResponseDto> CreateAsync(UsedVehiclesRequestDto request);
        Task<UsedVehiclesResponseDto?> UpdateAsync(string id, UsedVehiclesRequestDto request);
        Task<bool> DeleteAsync(string id);

        // Used Vehicles specific operations
        Task<bool> UpdatePriorityAsync(string id, int priority);
        Task<bool> DeactivateAsync(string id);
        Task<bool> MarkAsSoldAsync(string id);
        Task<bool> BulkUpdatePriorityAsync(Dictionary<string, int> priorityUpdates);

        // Engagement
        Task<bool> IncrementViewsAsync(string id);
        Task<bool> IncrementLikesAsync(string id);
        Task<bool> IncrementSharesAsync(string id);
        Task<bool> IncrementEnquiriesAsync(string id);

        // Rating
        Task<bool> AddRatingAsync(string id, UserRatingDto rating);

        // Validation
        Task<bool> IsSlugUniqueAsync(string slug, string? excludeId = null);
        Task<bool> IsModelSlugUniqueAsync(string modelSlug, string? excludeId = null);

        // Statistics
        Task<long> GetTotalCountAsync();
        Task<long> GetActiveCountAsync();
        Task<long> GetSoldCountAsync();
    }
}
