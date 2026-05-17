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
using System.Text.RegularExpressions;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Listings
{
    public class NewlyArrivedService : INewlyArrivedService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<NewlyArrivedService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);
        private const string CACHE_KEY_PREFIX = "newly_arrived_";

        public NewlyArrivedService(
            HttpClient httpClient,
            IMemoryCache memoryCache,
            ILogger<NewlyArrivedService> logger)
        {
            _httpClient = httpClient;
            _cache = memoryCache;
            _logger = logger;

            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _retryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => IsTransientError(r.StatusCode))
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(outcome.Exception,
                            "Retry {RetryCount} after {Delay}s due to {StatusCode}",
                            retryCount, timespan.TotalSeconds, outcome.Result?.StatusCode);
                    });
        }

        private bool IsTransientError(HttpStatusCode statusCode) =>
            statusCode == HttpStatusCode.InternalServerError ||
            statusCode == HttpStatusCode.ServiceUnavailable ||
            statusCode == HttpStatusCode.BadGateway ||
            statusCode == HttpStatusCode.GatewayTimeout ||
            statusCode == HttpStatusCode.RequestTimeout;

        private async Task<T?> ReadApiResponseAsync<T>(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("API request failed with status {StatusCode}: {Content}",
                    response.StatusCode, content);
                return default;
            }

            try
            {
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<T>>(content);

                if (apiResponse == null)
                {
                    _logger.LogWarning("Failed to deserialize API response");
                    return default;
                }

                if (!apiResponse.IsSuccess)
                {
                    _logger.LogWarning("API returned error: {Message}", apiResponse.Message);
                    return default;
                }

                return apiResponse.Data;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error for response: {Content}", content);
                return default;
            }
        }

        private void InvalidateAllCaches()
        {
            _logger.LogDebug("Invalidating all newly arrived caches");
        }

        private void InvalidateVehicleCache(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            _cache.Remove($"{CACHE_KEY_PREFIX}{id}");
            _cache.Remove($"{CACHE_KEY_PREFIX}slug_*");
            _logger.LogDebug("Invalidated cache for vehicle: {Id}", id);
        }

        private string GetPagedCacheKey(int page, int pageSize) =>
            $"{CACHE_KEY_PREFIX}all_{page}_{pageSize}";

        public async Task<PagedResult<NewlyArrivedResponseDto>> GetAllAsync(int page, int pageSize)
        {
            string cacheKey = GetPagedCacheKey(page, pageSize);

            if (_cache.TryGetValue(cacheKey, out PagedResult<NewlyArrivedResponseDto>? cached))
                return cached!;

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cached))
                    return cached!;

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/newlyarrived?page={page}&pageSize={pageSize}")
                );

                if (!response.IsSuccessStatusCode)
                    return new PagedResult<NewlyArrivedResponseDto>();

                var result = await ReadApiResponseAsync<PagedResult<NewlyArrivedResponseDto>>(response)
                             ?? new PagedResult<NewlyArrivedResponseDto>();

                _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                    SlidingExpiration = TimeSpan.FromMinutes(2),
                    Priority = CacheItemPriority.Normal
                });

                return result;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<NewlyArrivedResponseDto?> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            string cacheKey = $"{CACHE_KEY_PREFIX}{id}";

            if (_cache.TryGetValue(cacheKey, out NewlyArrivedResponseDto? cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/newlyarrived/{id}")
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var item = await ReadApiResponseAsync<NewlyArrivedResponseDto>(response);

            if (item != null)
            {
                _cache.Set(cacheKey, item, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(3),
                    Priority = CacheItemPriority.High
                });
            }

            return item;
        }

        public async Task<NewlyArrivedResponseDto?> GetByModelSlugAsync(string modelSlug)
        {
            if (string.IsNullOrWhiteSpace(modelSlug))
                return null;

            string cacheKey = $"{CACHE_KEY_PREFIX}slug_{modelSlug}";

            if (_cache.TryGetValue(cacheKey, out NewlyArrivedResponseDto? cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/newlyarrived/slug/{modelSlug}")
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var item = await ReadApiResponseAsync<NewlyArrivedResponseDto>(response);

            if (item != null)
            {
                _cache.Set(cacheKey, item, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(3)
                });
            }

            return item;
        }

        public async Task<IEnumerable<NewlyArrivedResponseDto>> GetFeaturedArrivalsAsync(int limit = 10)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}featured_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<NewlyArrivedResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/newlyarrived/featured?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<NewlyArrivedResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<NewlyArrivedResponseDto>>(response)
                        ?? Enumerable.Empty<NewlyArrivedResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            });

            return items;
        }

        public async Task<IEnumerable<NewlyArrivedResponseDto>> GetWeeklyArrivalsAsync(int limit = 20)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}weekly_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<NewlyArrivedResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/newlyarrived/weekly?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<NewlyArrivedResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<NewlyArrivedResponseDto>>(response)
                        ?? Enumerable.Empty<NewlyArrivedResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<NewlyArrivedResponseDto>> GetMonthlyArrivalsAsync(int month, int year, int limit = 20)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}monthly_{month}_{year}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<NewlyArrivedResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/newlyarrived/monthly?month={month}&year={year}&limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<NewlyArrivedResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<NewlyArrivedResponseDto>>(response)
                        ?? Enumerable.Empty<NewlyArrivedResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<NewlyArrivedResponseDto>> GetYearlyArrivalsAsync(int year)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}yearly_{year}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<NewlyArrivedResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/newlyarrived/yearly?year={year}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<NewlyArrivedResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<NewlyArrivedResponseDto>>(response)
                        ?? Enumerable.Empty<NewlyArrivedResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<NewlyArrivedResponseDto> CreateAsync(NewlyArrivedRequestDto request, string publishedBy)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync("api/v1/newlyarrived", request)
            );

            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new InvalidOperationException("Duplicate entry");

            response.EnsureSuccessStatusCode();

            var result = await ReadApiResponseAsync<NewlyArrivedResponseDto>(response)
                         ?? throw new Exception("Invalid response");

            InvalidateAllCaches();

            return result;
        }

        public async Task<NewlyArrivedResponseDto?> UpdateAsync(string id, NewlyArrivedRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PutAsJsonAsync($"api/v1/newlyarrived/{id}", request)
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var result = await ReadApiResponseAsync<NewlyArrivedResponseDto>(response);

            if (result != null)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
            }

            return result;
        }

        public async Task<bool> PublishAsync(string id, string publishedBy)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PatchAsync($"api/v1/newlyarrived/{id}/publish",
                    JsonContent.Create(new { publishedBy }))
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            return false;
        }

        public async Task<bool> UnpublishAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PatchAsync($"api/v1/newlyarrived/{id}/unpublish", null)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            return false;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.DeleteAsync($"api/v1/newlyarrived/{id}")
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            return false;
        }
    }
}