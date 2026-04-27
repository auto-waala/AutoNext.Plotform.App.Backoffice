using AutoNext.Plotform.App.Backoffice.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface IInspectionChecklistService
    {
        Task<InspectionChecklistResponseDto?> GetByIdAsync(Guid id);
        Task<IEnumerable<InspectionChecklistResponseDto>> GetAllAsync();
        Task<IEnumerable<InspectionChecklistResponseDto>> GetActiveAsync();
        Task<IEnumerable<InspectionChecklistResponseDto>> GetByCategoryAsync(string category);
        Task<InspectionChecklistResponseDto> CreateAsync(InspectionChecklistCreateDto dto);
        Task<InspectionChecklistResponseDto?> UpdateAsync(Guid id, InspectionChecklistUpdateDto dto);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ToggleStatusAsync(Guid id, bool isActive);
    }
}
