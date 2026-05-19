using AutoNext.Plotform.App.Backoffice.Integrations.Listings;
using AutoNext.Plotform.App.Backoffice.Models.Common;
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
    public class VehicleService : IVehicleService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<VehicleService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public VehicleService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<VehicleService> logger)
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
                            "Retry {RetryCount} after {Delay}s for Vehicle API due to: {StatusCode}",
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

        public async Task<PagedResult<VehicleDto>> GetAllAsync(int page, int pageSize)
        {
            string cacheKey = $"vehicles_all_{page}_{pageSize}";

            if (_cache.TryGetValue(cacheKey, out PagedResult<VehicleDto> cached))
            {
                _logger.LogDebug("Returning cached vehicles list for page {Page}", page);
                return cached;
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cached))
                    return cached;

                _logger.LogInformation("Fetching vehicles from API - page {Page}, size {PageSize}", page, pageSize);

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all vehicles", requestId);
                    return await _httpClient.GetAsync($"api/v1/vehicles?page={page}&pageSize={pageSize}");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get vehicles. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return new PagedResult<VehicleDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<PagedResult<VehicleDto>>(content);

                if (result != null)
                {
                    _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                        SlidingExpiration = TimeSpan.FromMinutes(10)
                    });
                }

                return result ?? new PagedResult<VehicleDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching vehicles");
                return new PagedResult<VehicleDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<VehicleDto?> GetByIdAsync(string id)
        {
            string cacheKey = $"vehicle_{id}";

            if (_cache.TryGetValue(cacheKey, out VehicleDto cached))
                return cached;

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/vehicles/{id}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var vehicle = JsonConvert.DeserializeObject<VehicleDto>(content);

                if (vehicle != null)
                {
                    _cache.Set(cacheKey, vehicle, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60),
                        SlidingExpiration = TimeSpan.FromMinutes(20)
                    });
                }

                return vehicle;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching vehicle by ID: {Id}", id);
                return null;
            }
        }

        public async Task<PagedResult<VehicleDto>> GetBySellerAsync(string sellerId, int page, int pageSize)
        {
            string cacheKey = $"vehicles_seller_{sellerId}_{page}_{pageSize}";

            if (_cache.TryGetValue(cacheKey, out PagedResult<VehicleDto> cached))
            {
                _logger.LogDebug("Returning cached vehicles for seller: {SellerId}", sellerId);
                return cached;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/vehicles/seller/{sellerId}?page={page}&pageSize={pageSize}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get vehicles for seller {SellerId}. Status: {StatusCode}", sellerId, response.StatusCode);
                    return new PagedResult<VehicleDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<PagedResult<VehicleDto>>(content);

                if (result != null)
                {
                    _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                        SlidingExpiration = TimeSpan.FromMinutes(10)
                    });
                }

                return result ?? new PagedResult<VehicleDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching vehicles for seller: {SellerId}", sellerId);
                return new PagedResult<VehicleDto>();
            }
        }

        public async Task<PagedResult<VehicleDto>> SearchAsync(VehicleSearchRequest request)
        {
            string cacheKey = $"vehicles_search_{JsonConvert.SerializeObject(request)}";

            if (_cache.TryGetValue(cacheKey, out PagedResult<VehicleDto> cached))
            {
                _logger.LogDebug("Returning cached search results");
                return cached;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/vehicles/search", request));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to search vehicles. Status: {StatusCode}", response.StatusCode);
                    return new PagedResult<VehicleDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<PagedResult<VehicleDto>>(content);

                if (result != null)
                {
                    _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return result ?? new PagedResult<VehicleDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching vehicles");
                return new PagedResult<VehicleDto>();
            }
        }

        public async Task<VehicleDto> CreateAsync(CreateVehicleRequest request, string sellerId)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/vehicles", request));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("A vehicle with these details already exists");

                response.EnsureSuccessStatusCode();

                InvalidateVehicleCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<VehicleDto>(content)!;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating vehicle");
                throw new InvalidOperationException("Failed to create vehicle. Please try again.", ex);
            }
        }

        public async Task<VehicleDto?> UpdateAsync(string id, UpdateVehicleRequest request)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/vehicles/{id}", request));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to update vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                InvalidateVehicleCaches();
                _cache.Remove($"vehicle_{id}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<VehicleDto>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating vehicle: {Id}", id);
                return null;
            }
        }

        public async Task IncrementViewsAsync(string id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/vehicles/{id}/views", null));

                if (!response.IsSuccessStatusCode)
                    _logger.LogWarning("Failed to increment views for vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
                else
                    _cache.Remove($"vehicle_{id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing views for vehicle: {Id}", id);
            }
        }

        public async Task<bool> DeleteAsync(string id, string deletedBy = "")
        {
            try
            {
                var url = string.IsNullOrEmpty(deletedBy)
                    ? $"api/v1/vehicles/{id}"
                    : $"api/v1/vehicles/{id}?deletedBy={deletedBy}";

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync(url));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateVehicleCaches();
                    _cache.Remove($"vehicle_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting vehicle: {Id}", id);
                return false;
            }
        }

        public async Task<bool> HardDeleteAsync(string id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/vehicles/{id}/hard"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateVehicleCaches();
                    _cache.Remove($"vehicle_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hard deleting vehicle: {Id}", id);
                return false;
            }
        }

        private void InvalidateVehicleCaches()
        {
            _cache.Remove("vehicles_all");
            _logger.LogDebug("Vehicle caches invalidated");
        }
    }
}