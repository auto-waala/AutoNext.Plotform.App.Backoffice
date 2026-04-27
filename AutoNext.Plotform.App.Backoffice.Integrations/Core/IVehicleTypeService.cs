using AutoNext.Plotform.App.Backoffice.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface IVehicleTypeService
    {
        Task<VehicleTypeResponseDto?> GetByIdAsync(Guid id);
        Task<IEnumerable<VehicleTypeResponseDto>> GetAllAsync(bool onlyActive = false);
        Task<VehicleTypeResponseDto> CreateAsync(VehicleTypeCreateDto createDto);
        Task<VehicleTypeResponseDto?> UpdateAsync(VehicleTypeUpdateDto updateDto);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ToggleActiveAsync(Guid id);
    }
}
