using AutoNext.Plotform.App.Backoffice.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface IFeatureService
    {
        Task<FeatureResponseDto?> GetByIdAsync(Guid id);
        Task<IEnumerable<FeatureResponseDto>> GetAllAsync();
        Task<IEnumerable<FeatureResponseDto>> GetActiveAsync();
        Task<IEnumerable<FeatureResponseDto>> GetByCategoryAsync(string category);
        Task<FeatureResponseDto> CreateAsync(FeatureCreateDto dto);
        Task<FeatureResponseDto?> UpdateAsync(Guid id, FeatureUpdateDto dto);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ToggleStatusAsync(Guid id, bool isActive);
    }
}
