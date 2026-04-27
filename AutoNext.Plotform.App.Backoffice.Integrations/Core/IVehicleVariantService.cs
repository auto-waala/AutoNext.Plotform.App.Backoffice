using AutoNext.Plotform.App.Backoffice.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface IVehicleVariantService
    {
        Task<VehicleVariantResponseDto?> GetVariantByIdAsync(Guid variantId);
        Task<VehicleVariantResponseDto?> GetVariantByCodeAsync(string code);
        Task<IEnumerable<VehicleVariantResponseDto>> GetAllVariantsAsync();
        Task<IEnumerable<VehicleVariantResponseDto>> GetActiveVariantsAsync();
        Task<IEnumerable<VehicleVariantResponseDto>> GetVariantsByModelAsync(Guid modelId);
        Task<IEnumerable<VehicleVariantResponseDto>> GetAvailableVariantsAsync();
        Task<IEnumerable<VehicleVariantResponseDto>> FilterVariantsAsync(VehicleVariantFilterDto filterDto);
        Task<VehicleVariantResponseDto> CreateVariantAsync(VehicleVariantCreateDto createDto);
        Task<VehicleVariantResponseDto?> UpdateVariantAsync(Guid variantId, VehicleVariantUpdateDto updateDto);
        Task<bool> DeleteVariantAsync(Guid variantId);
        Task<bool> ToggleVariantStatusAsync(Guid variantId, bool isActive);
        Task<bool> ToggleAvailabilityAsync(Guid variantId, bool isAvailable);
        Task<bool> ReorderVariantsAsync(Dictionary<Guid, int> orderMap);
        Task<VehicleVariantSpecsDto> GetVariantSpecsAsync(Guid variantId);
        Task<decimal> GetVariantPriceRangeAsync(Guid modelId);
    }
}
