using AutoNext.Plotform.App.Backoffice.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface ITransmissionService
    {
        Task<TransmissionResponseDto?> GetByIdAsync(Guid id);
        Task<IEnumerable<TransmissionResponseDto>> GetAllAsync(bool onlyActive = false);
        Task<TransmissionResponseDto> CreateAsync(TransmissionCreateDto createDto);
        Task<TransmissionResponseDto?> UpdateAsync(TransmissionUpdateDto updateDto);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ToggleActiveAsync(Guid id);
        Task<IEnumerable<TransmissionResponseDto>> GetByGearsCountAsync(int gearsCount);
    }
}
