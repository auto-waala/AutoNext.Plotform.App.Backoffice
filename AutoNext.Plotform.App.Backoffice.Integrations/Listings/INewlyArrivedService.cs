using AutoNext.Plotform.App.Backoffice.Models.Common;
using AutoNext.Plotform.App.Backoffice.Models.DTO;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Listings
{
    public interface INewlyArrivedService
    {
        // Get single
        Task<NewlyArrivedResponseDto?> GetByIdAsync(string id);
        Task<NewlyArrivedResponseDto?> GetByModelSlugAsync(string modelSlug);

        // Get collections
        Task<PagedResult<NewlyArrivedResponseDto>> GetAllAsync(int page, int pageSize);

        // Period-specific
        Task<IEnumerable<NewlyArrivedResponseDto>> GetWeeklyArrivalsAsync(int limit = 20);
        Task<IEnumerable<NewlyArrivedResponseDto>> GetMonthlyArrivalsAsync(int month, int year, int limit = 20);
        Task<IEnumerable<NewlyArrivedResponseDto>> GetYearlyArrivalsAsync(int year);

        // Featured
        Task<IEnumerable<NewlyArrivedResponseDto>> GetFeaturedArrivalsAsync(int limit = 10);

        // CRUD
        Task<NewlyArrivedResponseDto> CreateAsync(NewlyArrivedRequestDto request, string publishedBy);
        Task<NewlyArrivedResponseDto?> UpdateAsync(string id, NewlyArrivedRequestDto request);
        Task<bool> DeleteAsync(string id);
        Task<bool> PublishAsync(string id, string publishedBy);
        Task<bool> UnpublishAsync(string id);
    }
}
