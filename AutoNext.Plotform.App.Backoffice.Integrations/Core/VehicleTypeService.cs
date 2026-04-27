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
    public class VehicleTypeService : IVehicleTypeService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<VehicleTypeService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public VehicleTypeService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<VehicleTypeService> logger)
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
                            "Retry {RetryCount} after {Delay}s for VehicleType API due to: {StatusCode}",
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

        public async Task<IEnumerable<VehicleTypeResponseDto>> GetAllAsync(bool onlyActive = false)
        {
            string cacheKey = onlyActive ? "active_vehicle_types" : "all_vehicle_types";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<VehicleTypeResponseDto> cachedTypes))
            {
                _logger.LogDebug("Returning cached vehicle types (onlyActive: {OnlyActive})", onlyActive);
                return cachedTypes ?? Enumerable.Empty<VehicleTypeResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedTypes))
                {
                    return cachedTypes ?? Enumerable.Empty<VehicleTypeResponseDto>();
                }

                _logger.LogInformation("Fetching vehicle types from API (onlyActive: {OnlyActive})", onlyActive);

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get vehicle types (onlyActive: {OnlyActive})", requestId, onlyActive);
                    return await _httpClient.GetAsync($"api/v1/vehicletype{(onlyActive ? "/active" : "")}");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get vehicle types. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<VehicleTypeResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var types = JsonConvert.DeserializeObject<IEnumerable<VehicleTypeResponseDto>>(content);

                if (types != null && types.Any())
                {
                    _cache.Set(cacheKey, types, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return types ?? Enumerable.Empty<VehicleTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching vehicle types (onlyActive: {OnlyActive})", onlyActive);
                return Enumerable.Empty<VehicleTypeResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<VehicleTypeResponseDto?> GetByIdAsync(Guid id)
        {
            string cacheKey = $"vehicle_type_{id}";

            if (_cache.TryGetValue(cacheKey, out VehicleTypeResponseDto cachedType))
            {
                return cachedType;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/vehicletype/{id}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get vehicle type {VehicleTypeId}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var type = JsonConvert.DeserializeObject<VehicleTypeResponseDto>(content);

                if (type != null)
                {
                    _cache.Set(cacheKey, type, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return type;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching vehicle type by ID: {VehicleTypeId}", id);
                return null;
            }
        }

        public async Task<VehicleTypeResponseDto> CreateAsync(VehicleTypeCreateDto createDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/vehicletype", createDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Vehicle type with this name or code already exists");

                response.EnsureSuccessStatusCode();

                // Invalidate caches after create
                InvalidateVehicleTypeCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<VehicleTypeResponseDto>(content)!;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating vehicle type");
                throw new InvalidOperationException("Failed to create vehicle type. Please try again.", ex);
            }
        }

        public async Task<VehicleTypeResponseDto?> UpdateAsync(VehicleTypeUpdateDto updateDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync("api/v1/vehicletype", updateDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate vehicle type name or code");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Update failed for vehicle type. Status: {StatusCode}", response.StatusCode);
                    return null;
                }

                // Invalidate caches after update
                InvalidateVehicleTypeCaches();

                var content = await response.Content.ReadAsStringAsync();
                var updatedType = JsonConvert.DeserializeObject<VehicleTypeResponseDto>(content);

                // Remove specific item cache if ID is available
                if (updatedType?.Id != null)
                {
                    _cache.Remove($"vehicle_type_{updatedType.Id}");
                }

                return updatedType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating vehicle type");
                return null;
            }
        }

        public async Task<bool> ToggleActiveAsync(Guid id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/vehicletype/{id}/toggle-active", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateVehicleTypeCaches();
                    _cache.Remove($"vehicle_type_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling vehicle type active status: {VehicleTypeId}", id);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/vehicletype/{id}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateVehicleTypeCaches();
                    _cache.Remove($"vehicle_type_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting vehicle type: {VehicleTypeId}", id);
                return false;
            }
        }

        private void InvalidateVehicleTypeCaches()
        {
            _cache.Remove("all_vehicle_types");
            _cache.Remove("active_vehicle_types");
            _logger.LogDebug("Vehicle type caches invalidated");
        }
    }
}