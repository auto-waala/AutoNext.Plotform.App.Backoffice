using AutoNext.Plotform.App.Backoffice.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface ILocationService
    {
        Task<LocationResponseDto?> GetLocationByIdAsync(Guid locationId);
        Task<IEnumerable<LocationResponseDto>> GetAllLocationsAsync();
        Task<IEnumerable<LocationResponseDto>> GetLocationsByStateAsync(string stateCode);
        Task<IEnumerable<LocationResponseDto>> GetLocationsByCityAsync(string cityName);
        Task<LocationResponseDto> CreateLocationAsync(LocationCreateDto createDto);
        Task<bool> DeleteLocationAsync(Guid locationId);
        Task<IEnumerable<CityAreaDto>> GetAreasByCityAsync(Guid locationId);
    }
}
