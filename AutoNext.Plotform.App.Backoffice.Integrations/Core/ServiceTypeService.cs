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
    public class ServiceTypeService : IServiceTypeService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ServiceTypeService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public ServiceTypeService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<ServiceTypeService> logger)
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
                            "Retry {RetryCount} after {Delay}s for ServiceType API due to: {StatusCode}",
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

        public async Task<IEnumerable<ServiceTypeResponseDto>> GetAllAsync()
        {
            const string cacheKey = "all_service_types";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<ServiceTypeResponseDto> cachedTypes))
            {
                _logger.LogDebug("Returning cached service types");
                return cachedTypes ?? Enumerable.Empty<ServiceTypeResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedTypes))
                {
                    return cachedTypes ?? Enumerable.Empty<ServiceTypeResponseDto>();
                }

                _logger.LogInformation("Fetching all service types from API");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all service types", requestId);
                    return await _httpClient.GetAsync("api/v1/service-types");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get service types. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<ServiceTypeResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var types = JsonConvert.DeserializeObject<IEnumerable<ServiceTypeResponseDto>>(content);

                if (types != null && types.Any())
                {
                    _cache.Set(cacheKey, types, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return types ?? Enumerable.Empty<ServiceTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching service types");
                return Enumerable.Empty<ServiceTypeResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<IEnumerable<ServiceTypeResponseDto>> GetActiveAsync()
        {
            const string cacheKey = "active_service_types";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<ServiceTypeResponseDto> cachedTypes))
            {
                _logger.LogDebug("Returning cached active service types");
                return cachedTypes ?? Enumerable.Empty<ServiceTypeResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/service-types/active"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get active service types. Status: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<ServiceTypeResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var types = JsonConvert.DeserializeObject<IEnumerable<ServiceTypeResponseDto>>(content);

                if (types != null && types.Any())
                {
                    _cache.Set(cacheKey, types, TimeSpan.FromMinutes(10));
                }

                return types ?? Enumerable.Empty<ServiceTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active service types");
                return Enumerable.Empty<ServiceTypeResponseDto>();
            }
        }

        public async Task<ServiceTypeResponseDto?> GetByIdAsync(Guid id)
        {
            string cacheKey = $"service_type_{id}";

            if (_cache.TryGetValue(cacheKey, out ServiceTypeResponseDto cachedType))
            {
                return cachedType;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/service-types/{id}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get service type {ServiceTypeId}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var type = JsonConvert.DeserializeObject<ServiceTypeResponseDto>(content);

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
                _logger.LogError(ex, "Error fetching service type by ID: {ServiceTypeId}", id);
                return null;
            }
        }

        public async Task<ServiceTypeResponseDto> CreateAsync(ServiceTypeCreateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/service-types", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Service type with this code or name already exists");

                response.EnsureSuccessStatusCode();

                // Invalidate caches after create
                InvalidateServiceTypeCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ServiceTypeResponseDto>(content)!;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating service type");
                throw new InvalidOperationException("Failed to create service type. Please try again.", ex);
            }
        }

        public async Task<ServiceTypeResponseDto?> UpdateAsync(Guid id, ServiceTypeUpdateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/service-types/{id}", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate service type code or name");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Update failed for service type {ServiceTypeId}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                // Invalidate caches after update
                InvalidateServiceTypeCaches();
                _cache.Remove($"service_type_{id}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ServiceTypeResponseDto>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating service type: {ServiceTypeId}", id);
                return null;
            }
        }

        public async Task<bool> ToggleStatusAsync(Guid id, bool isActive)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/service-types/{id}/toggle/{isActive}", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateServiceTypeCaches();
                    _cache.Remove($"service_type_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling service type status: {ServiceTypeId}", id);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/service-types/{id}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateServiceTypeCaches();
                    _cache.Remove($"service_type_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting service type: {ServiceTypeId}", id);
                return false;
            }
        }

        private void InvalidateServiceTypeCaches()
        {
            _cache.Remove("all_service_types");
            _cache.Remove("active_service_types");
            _logger.LogDebug("Service type caches invalidated");
        }
    }
}