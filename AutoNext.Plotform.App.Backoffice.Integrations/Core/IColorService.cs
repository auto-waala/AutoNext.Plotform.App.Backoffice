using AutoNext.Plotform.App.Backoffice.Models.DTO;


namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public interface IColorService
    {
        Task<ColorResponseDto?> GetColorByIdAsync(Guid colorId);
        Task<ColorResponseDto?> GetColorByCodeAsync(string code);
        Task<IEnumerable<ColorResponseDto>> GetAllColorsAsync();
        Task<IEnumerable<ColorResponseDto>> GetActiveColorsAsync();
        Task<IEnumerable<ColorResponseDto>> GetColorsByDisplayOrderAsync();
        Task<ColorResponseDto> CreateColorAsync(ColorCreateDto createDto);
        Task<ColorResponseDto?> UpdateColorAsync(Guid colorId, ColorUpdateDto updateDto);
        Task<bool> DeleteColorAsync(Guid colorId);
        Task<bool> ToggleColorStatusAsync(Guid colorId, bool isActive);
        Task<bool> ReorderColorsAsync(Dictionary<Guid, int> orderMap);
        Task<bool> ValidateHexCodeAsync(string hexCode);
        Task<string?> ConvertHexToRgbAsync(string hexCode);
    }
}
