using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using System.Net;
using System.Net.Http.Json;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public class FuelTypeService : IFuelTypeService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<FuelTypeService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public FuelTypeService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<FuelTypeService> logger)
        {
            _httpClient = httpClient;
            _cache = memoryCache;
            _logger = logger;

            // Configure timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Retry policy for transient failures
            _retryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => IsTransientError(r.StatusCode))
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            outcome.Exception,
                            "Retry {RetryCount} after {Delay}s for FuelType API due to: {StatusCode}",
                            retryCount,
                            timespan.TotalSeconds,
                            outcome.Result?.StatusCode);
                    });
        }

        private bool IsTransientError(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.InternalServerError ||
                   statusCode == HttpStatusCode.ServiceUnavailable ||
                   statusCode == HttpStatusCode.BadGateway ||
                   statusCode == HttpStatusCode.GatewayTimeout ||
                   statusCode == HttpStatusCode.RequestTimeout;
        }

        public async Task<IEnumerable<FuelTypeResponseDto>> GetAllAsync(bool onlyActive = false)
        {
            string cacheKey = onlyActive ? "active_fuel_types" : "all_fuel_types";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FuelTypeResponseDto> cachedFuelTypes))
            {
                _logger.LogDebug("Returning cached fuel types (onlyActive: {OnlyActive})", onlyActive);
                return cachedFuelTypes ?? Enumerable.Empty<FuelTypeResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedFuelTypes))
                {
                    return cachedFuelTypes ?? Enumerable.Empty<FuelTypeResponseDto>();
                }

                _logger.LogInformation("Fetching fuel types from API (onlyActive: {OnlyActive})", onlyActive);

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get fuel types (onlyActive: {OnlyActive})", requestId, onlyActive);
                    var endpoint = onlyActive ? "api/v1/fueltypes/active" : "api/v1/fueltypes";
                    return await _httpClient.GetAsync(endpoint);
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get fuel types. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<FuelTypeResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var fuelTypes = JsonConvert.DeserializeObject<IEnumerable<FuelTypeResponseDto>>(content);

                if (fuelTypes != null && fuelTypes.Any())
                {
                    _cache.Set(cacheKey, fuelTypes, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return fuelTypes ?? Enumerable.Empty<FuelTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching fuel types (onlyActive: {OnlyActive})", onlyActive);
                return Enumerable.Empty<FuelTypeResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<FuelTypeResponseDto?> GetByIdAsync(Guid id)
        {
            string cacheKey = $"fuel_type_{id}";

            if (_cache.TryGetValue(cacheKey, out FuelTypeResponseDto cachedFuelType))
            {
                return cachedFuelType;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/fueltypes/{id}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get fuel type {FuelTypeId}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var fuelType = JsonConvert.DeserializeObject<FuelTypeResponseDto>(content);

                if (fuelType != null)
                {
                    _cache.Set(cacheKey, fuelType, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                        SlidingExpiration = TimeSpan.FromMinutes(10)
                    });
                }

                return fuelType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching fuel type by ID: {FuelTypeId}", id);
                return null;
            }
        }

        public async Task<FuelTypeResponseDto> CreateAsync(FuelTypeCreateDto createDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/fueltypes", createDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Fuel type with this code or name already exists");

                response.EnsureSuccessStatusCode();

                // Invalidate caches after create
                InvalidateFuelTypeCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<FuelTypeResponseDto>(content)!;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating fuel type");
                throw new InvalidOperationException("Failed to create fuel type. Please try again.", ex);
            }
        }

        public async Task<FuelTypeResponseDto?> UpdateAsync(FuelTypeUpdateDto updateDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync("api/v1/fueltypes", updateDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate fuel type code or name");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Update failed for fuel type {FuelTypeId}. Status: {StatusCode}", updateDto.Id, response.StatusCode);
                    return null;
                }

                // Invalidate caches after update
                InvalidateFuelTypeCaches();
                _cache.Remove($"fuel_type_{updateDto.Id}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<FuelTypeResponseDto>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating fuel type: {FuelTypeId}", updateDto.Id);
                return null;
            }
        }

        public async Task<bool> ToggleActiveAsync(Guid id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/fueltypes/{id}/toggle-active", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateFuelTypeCaches();
                    _cache.Remove($"fuel_type_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling fuel type active status: {FuelTypeId}", id);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/fueltypes/{id}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateFuelTypeCaches();
                    _cache.Remove($"fuel_type_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting fuel type: {FuelTypeId}", id);
                return false;
            }
        }

        private void InvalidateFuelTypeCaches()
        {
            _cache.Remove("all_fuel_types");
            _cache.Remove("active_fuel_types");
            _logger.LogDebug("Fuel type caches invalidated");
        }
    }
}