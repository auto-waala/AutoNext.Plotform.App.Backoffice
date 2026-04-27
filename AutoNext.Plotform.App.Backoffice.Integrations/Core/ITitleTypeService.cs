using AutoNext.Plotform.App.Backoffice.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface ITitleTypeService
    {
        Task<IEnumerable<TitleTypeResponseDto>> GetAllAsync();
        Task<IEnumerable<TitleTypeResponseDto>> GetActiveAsync();
        Task<TitleTypeResponseDto?> GetByIdAsync(Guid id);
        Task<TitleTypeResponseDto> CreateAsync(TitleTypeCreateDto dto);
        Task<TitleTypeResponseDto?> UpdateAsync(Guid id, TitleTypeUpdateDto dto);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ToggleStatusAsync(Guid id, bool isActive);
    }
}
