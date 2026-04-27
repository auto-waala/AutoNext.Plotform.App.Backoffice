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
    public class VehicleConditionService : IVehicleConditionService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<VehicleConditionService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public VehicleConditionService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<VehicleConditionService> logger)
        {
            _httpClient = httpClient;
            _cache = memoryCache;
            _logger = logger;

            _httpClient.Timeout = TimeSpan.FromSeconds(30);

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
                            "Retry {RetryCount} after {Delay}s for VehicleCondition API due to: {StatusCode}",
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

        public async Task<IEnumerable<VehicleConditionResponseDto>> GetAllAsync()
        {
            const string cacheKey = "all_vehicle_conditions";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<VehicleConditionResponseDto> cachedConditions))
            {
                _logger.LogDebug("Returning cached vehicle conditions");
                return cachedConditions ?? Enumerable.Empty<VehicleConditionResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedConditions))
                    return cachedConditions ?? Enumerable.Empty<VehicleConditionResponseDto>();

                _logger.LogInformation("Fetching all vehicle conditions from API");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all conditions", requestId);
                    return await _httpClient.GetAsync("api/v1/vehicle-conditions");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get conditions. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<VehicleConditionResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var conditions = JsonConvert.DeserializeObject<IEnumerable<VehicleConditionResponseDto>>(content);

                if (conditions != null && conditions.Any())
                {
                    _cache.Set(cacheKey, conditions, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    });
                }

                return conditions ?? Enumerable.Empty<VehicleConditionResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching vehicle conditions");
                return Enumerable.Empty<VehicleConditionResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<IEnumerable<VehicleConditionResponseDto>> GetActiveAsync()
        {
            const string cacheKey = "active_vehicle_conditions";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<VehicleConditionResponseDto> cachedConditions))
                return cachedConditions ?? Enumerable.Empty<VehicleConditionResponseDto>();

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/vehicle-conditions/active"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get active conditions. Status: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<VehicleConditionResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var conditions = JsonConvert.DeserializeObject<IEnumerable<VehicleConditionResponseDto>>(content);

                if (conditions != null && conditions.Any())
                {
                    _cache.Set(cacheKey, conditions, TimeSpan.FromMinutes(5));
                }

                return conditions ?? Enumerable.Empty<VehicleConditionResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active vehicle conditions");
                return Enumerable.Empty<VehicleConditionResponseDto>();
            }
        }

        public async Task<VehicleConditionResponseDto?> GetByIdAsync(Guid id)
        {
            string cacheKey = $"vehicle_condition_{id}";

            if (_cache.TryGetValue(cacheKey, out VehicleConditionResponseDto cachedCondition))
                return cachedCondition;

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/vehicle-conditions/{id}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get condition by ID {Id}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var condition = JsonConvert.DeserializeObject<VehicleConditionResponseDto>(content);

                if (condition != null)
                {
                    _cache.Set(cacheKey, condition, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return condition;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching vehicle condition by ID: {Id}", id);
                return null;
            }
        }

        public async Task<VehicleConditionResponseDto> CreateAsync(VehicleConditionCreateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/vehicle-conditions", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("A vehicle condition with this code or name already exists.");

                response.EnsureSuccessStatusCode();

                InvalidateConditionCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<VehicleConditionResponseDto>(content)!;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating vehicle condition");
                throw new InvalidOperationException("Failed to create vehicle condition. Please try again.", ex);
            }
        }

        public async Task<VehicleConditionResponseDto?> UpdateAsync(Guid id, VehicleConditionUpdateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/vehicle-conditions/{id}", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate condition code or name.");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to update vehicle condition {Id}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                InvalidateConditionCaches();
                _cache.Remove($"vehicle_condition_{id}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<VehicleConditionResponseDto>(content);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating vehicle condition: {Id}", id);
                return null;
            }
        }

        public async Task<bool> ToggleStatusAsync(Guid id, bool isActive)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/vehicle-conditions/{id}/toggle/{isActive}", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateConditionCaches();
                    _cache.Remove($"vehicle_condition_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling vehicle condition status: {Id}", id);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/vehicle-conditions/{id}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateConditionCaches();
                    _cache.Remove($"vehicle_condition_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting vehicle condition: {Id}", id);
                return false;
            }
        }

        private void InvalidateConditionCaches()
        {
            _cache.Remove("all_vehicle_conditions");
            _cache.Remove("active_vehicle_conditions");
        }
    }
}