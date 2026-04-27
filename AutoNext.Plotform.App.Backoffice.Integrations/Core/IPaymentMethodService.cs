using AutoNext.Plotform.App.Backoffice.Models.DTO;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface IPaymentMethodService
    {
        Task<PaymentMethodResponseDto?> GetByIdAsync(Guid id);
        Task<IEnumerable<PaymentMethodResponseDto>> GetAllAsync();
        Task<IEnumerable<PaymentMethodResponseDto>> GetActiveAsync();
        Task<IEnumerable<PaymentMethodResponseDto>> GetByTypeAsync(string type);
        Task<PaymentMethodResponseDto> CreateAsync(PaymentMethodCreateDto dto);
        Task<PaymentMethodResponseDto?> UpdateAsync(Guid id, PaymentMethodUpdateDto dto);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ToggleStatusAsync(Guid id, bool isActive);
    }
}
