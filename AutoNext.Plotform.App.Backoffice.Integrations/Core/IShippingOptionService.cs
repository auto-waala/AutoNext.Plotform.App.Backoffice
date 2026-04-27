using AutoNext.Plotform.App.Backoffice.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface IShippingOptionService
    {
        Task<IEnumerable<ShippingOptionResponseDto>> GetAllAsync();
        Task<IEnumerable<ShippingOptionResponseDto>> GetActiveAsync();
        Task<ShippingOptionResponseDto?> GetByIdAsync(Guid id);
        Task<ShippingOptionResponseDto> CreateAsync(ShippingOptionCreateDto dto);
        Task<ShippingOptionResponseDto?> UpdateAsync(Guid id, ShippingOptionUpdateDto dto);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ToggleStatusAsync(Guid id, bool isActive);
    }
}
