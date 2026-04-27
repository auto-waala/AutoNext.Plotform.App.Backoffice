using AutoNext.Plotform.App.Backoffice.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface IVehicleConditionService
    {
        Task<IEnumerable<VehicleConditionResponseDto>> GetAllAsync();
        Task<IEnumerable<VehicleConditionResponseDto>> GetActiveAsync();
        Task<VehicleConditionResponseDto?> GetByIdAsync(Guid id);
        Task<VehicleConditionResponseDto> CreateAsync(VehicleConditionCreateDto dto);
        Task<VehicleConditionResponseDto?> UpdateAsync(Guid id, VehicleConditionUpdateDto dto);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ToggleStatusAsync(Guid id, bool isActive);
    }
}
