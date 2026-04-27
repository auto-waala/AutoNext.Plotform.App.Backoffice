using AutoNext.Plotform.App.Backoffice.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface IServiceTypeService
    {
        Task<IEnumerable<ServiceTypeResponseDto>> GetAllAsync();

        Task<IEnumerable<ServiceTypeResponseDto>> GetActiveAsync();

        Task<ServiceTypeResponseDto?> GetByIdAsync(Guid id);

        Task<ServiceTypeResponseDto> CreateAsync(ServiceTypeCreateDto dto);

        Task<ServiceTypeResponseDto?> UpdateAsync(Guid id, ServiceTypeUpdateDto dto);

        Task<bool> DeleteAsync(Guid id);

        Task<bool> ToggleStatusAsync(Guid id, bool isActive);
    }
}
