using AutoNext.Plotform.App.Backoffice.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface IFuelTypeService
    {
        Task<FuelTypeResponseDto?> GetByIdAsync(Guid id);
        Task<IEnumerable<FuelTypeResponseDto>> GetAllAsync(bool onlyActive = false);
        Task<FuelTypeResponseDto> CreateAsync(FuelTypeCreateDto createDto);
        Task<FuelTypeResponseDto?> UpdateAsync(FuelTypeUpdateDto updateDto);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ToggleActiveAsync(Guid id);
    }
}
