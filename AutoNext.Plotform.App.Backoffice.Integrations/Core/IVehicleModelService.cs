using AutoNext.Plotform.App.Backoffice.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface IVehicleModelService
    {
        Task<VehicleModelResponseDto?> GetByIdAsync(Guid id);
        Task<IEnumerable<VehicleModelResponseDto>> GetAllAsync();
        Task<IEnumerable<VehicleModelResponseDto>> GetByBrandAsync(Guid brandId);
        Task<VehicleModelResponseDto> CreateAsync(VehicleModelCreateDto dto);
        Task<VehicleModelResponseDto?> UpdateAsync(Guid id, VehicleModelUpdateDto dto);
        Task<bool> DeleteAsync(Guid id);
    }
}
