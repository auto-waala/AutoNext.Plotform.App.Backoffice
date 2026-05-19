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
        private const string BASE_PATH = "api/v1/newlyarrived";
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
                            "Retry {RetryCount} after {Delay:F1}s due to {StatusCode} for {Url}",
                            retryCount, timespan.TotalSeconds, outcome.Result?.StatusCode,
                            outcome.Result?.RequestMessage?.RequestUri);
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

            _logger.LogDebug("Response Status: {StatusCode}, Content Preview: {ContentPreview}",
                response.StatusCode,
                content?.Length > 500 ? content.Substring(0, 500) : content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("API request failed with status {StatusCode}. URL: {Url}, Content: {Content}",
                    response.StatusCode, response.RequestMessage?.RequestUri, content);
                return default;
            }

            try
            {
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<T>>(content);

                if (apiResponse == null)
                {
                    _logger.LogWarning("Failed to deserialize API response. Content: {Content}", content);
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

                var fullUrl = $"{BASE_PATH}?page={page}&pageSize={pageSize}";
                _logger.LogInformation("Calling NewlyArrived API: {Url}", fullUrl);

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync(fullUrl)
                );

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get newly arrived vehicles. Status: {StatusCode}", response.StatusCode);
                    return new PagedResult<NewlyArrivedResponseDto>();
                }

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

            var fullUrl = $"{BASE_PATH}/{id}";
            _logger.LogInformation("Calling NewlyArrived API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get newly arrived vehicle by ID {Id}. Status: {StatusCode}", id, response.StatusCode);
                return null;
            }

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

            var fullUrl = $"{BASE_PATH}/slug/{Uri.EscapeDataString(modelSlug)}";
            _logger.LogInformation("Calling NewlyArrived API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get newly arrived vehicle by model slug {ModelSlug}. Status: {StatusCode}", modelSlug, response.StatusCode);
                return null;
            }

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

            var fullUrl = $"{BASE_PATH}/featured?limit={limit}";
            _logger.LogInformation("Calling NewlyArrived API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get featured arrivals. Status: {StatusCode}", response.StatusCode);
                return Enumerable.Empty<NewlyArrivedResponseDto>();
            }

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

            var fullUrl = $"{BASE_PATH}/weekly?limit={limit}";
            _logger.LogInformation("Calling NewlyArrived API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get weekly arrivals. Status: {StatusCode}", response.StatusCode);
                return Enumerable.Empty<NewlyArrivedResponseDto>();
            }

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

            var fullUrl = $"{BASE_PATH}/monthly?month={month}&year={year}&limit={limit}";
            _logger.LogInformation("Calling NewlyArrived API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get monthly arrivals for {Month}/{Year}. Status: {StatusCode}", month, year, response.StatusCode);
                return Enumerable.Empty<NewlyArrivedResponseDto>();
            }

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

            var fullUrl = $"{BASE_PATH}/yearly?year={year}";
            _logger.LogInformation("Calling NewlyArrived API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get yearly arrivals for {Year}. Status: {StatusCode}", year, response.StatusCode);
                return Enumerable.Empty<NewlyArrivedResponseDto>();
            }

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

            _logger.LogInformation("Creating new newly arrived vehicle by {PublishedBy}", publishedBy);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync(BASE_PATH, request)
            );

            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new InvalidOperationException("Duplicate entry - A vehicle with this model slug already exists");

            response.EnsureSuccessStatusCode();

            var result = await ReadApiResponseAsync<NewlyArrivedResponseDto>(response)
                         ?? throw new Exception("Invalid response from API");

            InvalidateAllCaches();

            return result;
        }

        public async Task<NewlyArrivedResponseDto?> UpdateAsync(string id, NewlyArrivedRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            _logger.LogInformation("Updating newly arrived vehicle {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PutAsJsonAsync($"{BASE_PATH}/{id}", request)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to update newly arrived vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
                return null;
            }

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

            _logger.LogInformation("Publishing newly arrived vehicle {Id} by {PublishedBy}", id, publishedBy);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PatchAsync($"{BASE_PATH}/{id}/publish",
                    JsonContent.Create(new { publishedBy }))
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            _logger.LogWarning("Failed to publish newly arrived vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        public async Task<bool> UnpublishAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            _logger.LogInformation("Unpublishing newly arrived vehicle {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PatchAsync($"{BASE_PATH}/{id}/unpublish", null)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            _logger.LogWarning("Failed to unpublish newly arrived vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            _logger.LogInformation("Deleting newly arrived vehicle {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.DeleteAsync($"{BASE_PATH}/{id}")
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            _logger.LogWarning("Failed to delete newly arrived vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }
    }
}