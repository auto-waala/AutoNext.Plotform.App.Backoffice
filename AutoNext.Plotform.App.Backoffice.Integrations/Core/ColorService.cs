using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Newtonsoft.Json;
using System.Net.Http.Json;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public class ColorService : IColorService
    {
        private readonly HttpClient _httpClient;

        public ColorService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IEnumerable<ColorResponseDto>> GetAllColorsAsync()
        {
            var response = await _httpClient.GetAsync("api/v1/colors");

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<ColorResponseDto>();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IEnumerable<ColorResponseDto>>(content)
                   ?? Enumerable.Empty<ColorResponseDto>();
        }

        public async Task<IEnumerable<ColorResponseDto>> GetActiveColorsAsync()
        {
            var response = await _httpClient.GetAsync("api/v1/colors/active");

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<ColorResponseDto>();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IEnumerable<ColorResponseDto>>(content)
                   ?? Enumerable.Empty<ColorResponseDto>();
        }

        public async Task<IEnumerable<ColorResponseDto>> GetColorsByDisplayOrderAsync()
        {
            var response = await _httpClient.GetAsync("api/v1/colors/ordered");

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<ColorResponseDto>();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IEnumerable<ColorResponseDto>>(content)
                   ?? Enumerable.Empty<ColorResponseDto>();
        }

        public async Task<ColorResponseDto?> GetColorByIdAsync(Guid colorId)
        {
            var response = await _httpClient.GetAsync($"api/v1/colors/{colorId}");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ColorResponseDto>(content);
        }

        public async Task<ColorResponseDto?> GetColorByCodeAsync(string code)
        {
            var response = await _httpClient.GetAsync($"api/v1/colors/code/{code}");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ColorResponseDto>(content);
        }

        public async Task<ColorResponseDto> CreateColorAsync(ColorCreateDto createDto)
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/colors", createDto);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                throw new InvalidOperationException("Color already exists");

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ColorResponseDto>(content)!;
        }

        public async Task<ColorResponseDto?> UpdateColorAsync(Guid colorId, ColorUpdateDto updateDto)
        {
            var response = await _httpClient.PutAsJsonAsync($"api/v1/colors/{colorId}", updateDto);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                throw new InvalidOperationException("Duplicate color code");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ColorResponseDto>(content);
        }

        public async Task<bool> ToggleColorStatusAsync(Guid colorId, bool isActive)
        {
            var response = await _httpClient.PatchAsync(
                $"api/v1/colors/{colorId}/toggle/{isActive}",
                null
            );

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> ReorderColorsAsync(Dictionary<Guid, int> orderMap)
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/colors/reorder", orderMap);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> ValidateHexCodeAsync(string hexCode)
        {
            var response = await _httpClient.GetAsync($"api/v1/colors/validate-hex?hexCode={hexCode}");

            if (!response.IsSuccessStatusCode)
                return false;

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(content);

            return result?.isValid ?? false;
        }

        public async Task<string?> ConvertHexToRgbAsync(string hexCode)
        {
            var response = await _httpClient.GetAsync($"api/v1/colors/convert-hex-to-rgb?hexCode={hexCode}");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(content);

            return result?.rgbValue;
        }

        public async Task<bool> DeleteColorAsync(Guid colorId)
        {
            var response = await _httpClient.DeleteAsync($"api/v1/colors/{colorId}");
            return response.IsSuccessStatusCode;
        }
    }
}
