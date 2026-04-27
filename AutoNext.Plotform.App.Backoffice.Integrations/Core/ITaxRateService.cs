using AutoNext.Plotform.App.Backoffice.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface ITaxRateService
    {
        Task<IEnumerable<TaxRateResponseDto>> GetAllAsync();
        Task<IEnumerable<TaxRateResponseDto>> GetActiveAsync();
        Task<TaxRateResponseDto?> GetByIdAsync(Guid id);
        Task<TaxRateResponseDto> CreateAsync(TaxRateCreateDto dto);
        Task<TaxRateResponseDto?> UpdateAsync(Guid id, TaxRateUpdateDto dto);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ToggleStatusAsync(Guid id, bool isActive);
    }
}
