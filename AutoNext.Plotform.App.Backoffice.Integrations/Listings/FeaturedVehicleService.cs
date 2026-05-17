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
    public class FeaturedVehicleService : IFeaturedVehicleService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<FeaturedVehicleService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);
        private const string BASE_PATH = "api/v1/FeaturedVehicle";
        private const string CACHE_KEY_PREFIX = "featured_vehicle_";

        public FeaturedVehicleService(
            HttpClient httpClient,
            IMemoryCache memoryCache,
            ILogger<FeaturedVehicleService> logger)
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
                            "Retry {RetryCount} after {Delay}s due to {StatusCode} for {Url}",
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

        /// <summary>
        /// Invalidates all cache entries related to featured vehicles
        /// </summary>
        private void InvalidateAllCaches()
        {
            _logger.LogDebug("Invalidating all featured vehicle caches");
            // Note: IMemoryCache doesn't support pattern removal directly
            // We'll rely on individual key removal and short expiration times
            // For production, consider using IDistributedCache with Redis
        }

        /// <summary>
        /// Invalidates cache for a specific vehicle by ID
        /// </summary>
        private void InvalidateVehicleCache(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            _cache.Remove($"{CACHE_KEY_PREFIX}{id}");
            _cache.Remove($"{CACHE_KEY_PREFIX}slug_*");
            _cache.Remove($"{CACHE_KEY_PREFIX}model_*");
            _logger.LogDebug("Invalidated cache for vehicle: {Id}", id);
        }

        private string GetPagedCacheKey(int page, int pageSize, string? sortBy, string? sortOrder) =>
            $"{CACHE_KEY_PREFIX}all_{page}_{pageSize}_{sortBy ?? "none"}_{sortOrder ?? "none"}";

        // ── GET Operations ─────────────────────────────────────────────────────────

        public async Task<PagedResult<FeaturedVehicleResponseDto>> GetAllAsync(
            int page, int pageSize, string? sortBy = null, string? sortOrder = null)
        {
            string cacheKey = GetPagedCacheKey(page, pageSize, sortBy, sortOrder);

            if (_cache.TryGetValue(cacheKey, out PagedResult<FeaturedVehicleResponseDto>? cached))
                return cached!;

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cached))
                    return cached!;

                var queryString = BuildQueryString(page, pageSize, sortBy, sortOrder);
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"{BASE_PATH}{queryString}")
                );

                if (!response.IsSuccessStatusCode)
                    return new PagedResult<FeaturedVehicleResponseDto>();

                var result = await ReadApiResponseAsync<PagedResult<FeaturedVehicleResponseDto>>(response)
                             ?? new PagedResult<FeaturedVehicleResponseDto>();

                _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), // Shorter for lists
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

        private string BuildQueryString(int page, int pageSize, string? sortBy, string? sortOrder)
        {
            var queryParams = new List<string>
            {
                $"page={page}",
                $"pageSize={pageSize}"
            };

            if (!string.IsNullOrEmpty(sortBy))
                queryParams.Add($"sortBy={Uri.EscapeDataString(sortBy)}");

            if (!string.IsNullOrEmpty(sortOrder))
                queryParams.Add($"sortOrder={Uri.EscapeDataString(sortOrder)}");

            return $"?{string.Join("&", queryParams)}";
        }

        public async Task<FeaturedVehicleResponseDto?> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            string cacheKey = $"{CACHE_KEY_PREFIX}{id}";

            if (_cache.TryGetValue(cacheKey, out FeaturedVehicleResponseDto? cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}")
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var item = await ReadApiResponseAsync<FeaturedVehicleResponseDto>(response);

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

        public async Task<FeaturedVehicleResponseDto?> GetBySlugAsync(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return null;

            string cacheKey = $"{CACHE_KEY_PREFIX}slug_{slug}";

            if (_cache.TryGetValue(cacheKey, out FeaturedVehicleResponseDto? cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/slug/{Uri.EscapeDataString(slug)}")
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var item = await ReadApiResponseAsync<FeaturedVehicleResponseDto>(response);

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

        public async Task<FeaturedVehicleResponseDto?> GetByModelSlugAsync(string modelSlug)
        {
            if (string.IsNullOrWhiteSpace(modelSlug))
                return null;

            string cacheKey = $"{CACHE_KEY_PREFIX}model_{modelSlug}";

            if (_cache.TryGetValue(cacheKey, out FeaturedVehicleResponseDto? cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/model/{Uri.EscapeDataString(modelSlug)}")
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var item = await ReadApiResponseAsync<FeaturedVehicleResponseDto>(response);

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

        // ── CRUD Operations with Cache Invalidation ─────────────────────────────────

        public async Task<FeaturedVehicleResponseDto> CreateAsync(FeaturedVehicleRequestDto request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync(BASE_PATH, request)
            );

            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new InvalidOperationException("A featured vehicle with this model already exists");

            response.EnsureSuccessStatusCode();

            var result = await ReadApiResponseAsync<FeaturedVehicleResponseDto>(response)
                         ?? throw new Exception("Failed to create featured vehicle: Invalid response from API");

            // Invalidate all caches on create
            InvalidateAllCaches();

            return result;
        }

        public async Task<FeaturedVehicleResponseDto?> UpdateAsync(string id, FeaturedVehicleRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PutAsJsonAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}", request)
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var result = await ReadApiResponseAsync<FeaturedVehicleResponseDto>(response);

            if (result != null)
            {
                // Invalidate specific vehicle cache
                InvalidateVehicleCache(id);
                // Invalidate list caches
                InvalidateAllCaches();
            }

            return result;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.DeleteAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}")
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            return false;
        }

        // ── Featured-specific Operations with Cache Invalidation ─────────────────

        public async Task<bool> UpdatePriorityAsync(string id, int priority)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            if (priority < 0)
                throw new ArgumentException("Priority must be non-negative", nameof(priority));

            var request = new UpdatePriorityRequest { Priority = priority };

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PatchAsJsonAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}/priority", request)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            return false;
        }

        public async Task<bool> BulkUpdatePriorityAsync(Dictionary<string, int> priorityUpdates)
        {
            if (priorityUpdates == null || priorityUpdates.Count == 0)
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync($"{BASE_PATH}/bulk/priority", priorityUpdates)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateAllCaches();
                return true;
            }

            return false;
        }

        public async Task<bool> ActivateAsync(string id, DateTime? endDate = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var request = endDate.HasValue
                ? new ActivateFeaturedVehicleRequest { EndDate = endDate }
                : null;

            var content = request != null
                ? JsonContent.Create(request)
                : null;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}/activate", content)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            return false;
        }

        public async Task<bool> DeactivateAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}/deactivate", null)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            return false;
        }

        // ── Filtered Collections (with shorter cache durations) ─────────────────

        public async Task<IEnumerable<FeaturedVehicleSummaryDto>> GetActiveFeaturedVehiclesAsync(int limit = 50)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}active_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeaturedVehicleSummaryDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/active?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();

            var items = await ReadApiResponseAsync<IEnumerable<FeaturedVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<FeaturedVehicleSummaryDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            });

            return items;
        }

        public async Task<IEnumerable<FeaturedVehicleSummaryDto>> GetTopPriorityFeaturedVehiclesAsync(int limit = 10)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}top_priority_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeaturedVehicleSummaryDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/top-priority?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();

            var items = await ReadApiResponseAsync<IEnumerable<FeaturedVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<FeaturedVehicleSummaryDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            });

            return items;
        }

        // ── Engagement Operations (no cache invalidation needed) ─────────────────

        public async Task<bool> IncrementViewsAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}/view", null)
            );

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> IncrementLikesAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}/like", null)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                return true;
            }

            return false;
        }

        public async Task<bool> IncrementSharesAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}/share", null)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                return true;
            }

            return false;
        }

        public async Task<bool> IncrementEnquiriesAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}/enquire", null)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                return true;
            }

            return false;
        }

        public async Task<bool> AddRatingAsync(string id, UserRatingDto rating)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            if (rating == null)
                throw new ArgumentNullException(nameof(rating));

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}/rating", rating)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                return true;
            }

            return false;
        }

        public async Task<IEnumerable<FeaturedVehicleSummaryDto>> GetByBrandAsync(string brandName, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(brandName))
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}brand_{brandName}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeaturedVehicleSummaryDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/brand/{Uri.EscapeDataString(brandName)}?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();

            var items = await ReadApiResponseAsync<IEnumerable<FeaturedVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<FeaturedVehicleSummaryDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<FeaturedVehicleSummaryDto>> GetByVehicleTypeAsync(string vehicleType, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(vehicleType))
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}type_{vehicleType}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeaturedVehicleSummaryDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/type/{Uri.EscapeDataString(vehicleType)}?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();

            var items = await ReadApiResponseAsync<IEnumerable<FeaturedVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<FeaturedVehicleSummaryDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<FeaturedVehicleSummaryDto>> GetByPriceRangeAsync(decimal minPrice, decimal maxPrice, int limit = 20)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}price_{minPrice}_{maxPrice}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeaturedVehicleSummaryDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/price-range?minPrice={minPrice}&maxPrice={maxPrice}&limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();

            var items = await ReadApiResponseAsync<IEnumerable<FeaturedVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<FeaturedVehicleSummaryDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<FeaturedVehicleSummaryDto>> GetByCityAsync(string city, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(city))
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}city_{city}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeaturedVehicleSummaryDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/city/{Uri.EscapeDataString(city)}?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();

            var items = await ReadApiResponseAsync<IEnumerable<FeaturedVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<FeaturedVehicleSummaryDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<FeaturedVehicleSummaryDto>> SearchAsync(string searchTerm, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}search_{searchTerm}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeaturedVehicleSummaryDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/search?q={Uri.EscapeDataString(searchTerm)}&limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();

            var items = await ReadApiResponseAsync<IEnumerable<FeaturedVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<FeaturedVehicleSummaryDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }
    }
}