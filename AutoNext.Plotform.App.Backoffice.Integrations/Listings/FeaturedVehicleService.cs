using AutoNext.Plotform.App.Backoffice.Models.Common;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using System.Collections.Concurrent;
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

        // Track all cache keys for this service instance
        private readonly ConcurrentDictionary<string, byte> _trackedCacheKeys = new ConcurrentDictionary<string, byte>();
        private readonly SemaphoreSlim _trackedKeysLock = new SemaphoreSlim(1, 1);

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

        /// <summary>
        /// Invalidates all cache entries tracked by this service instance
        /// </summary>
        private async Task InvalidateAllCachesAsync()
        {
            _logger.LogInformation("Invalidating all featured vehicle caches. Total tracked keys: {Count}", _trackedCacheKeys.Count);

            await _trackedKeysLock.WaitAsync();
            try
            {
                foreach (var key in _trackedCacheKeys.Keys)
                {
                    _cache.Remove(key);
                }
                _trackedCacheKeys.Clear();
            }
            finally
            {
                _trackedKeysLock.Release();
            }
        }

        /// <summary>
        /// Invalidates cache for a specific vehicle by ID and all related caches
        /// </summary>
        private async Task InvalidateVehicleCachesAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            _logger.LogDebug("Invalidating all caches for vehicle: {Id}", id);

            await _trackedKeysLock.WaitAsync();
            try
            {
                var keysToRemove = _trackedCacheKeys.Keys
                    .Where(key => key.Contains(id) ||
                                  key.StartsWith(CACHE_KEY_PREFIX) && (
                                      key.Contains("all_") ||
                                      key.Contains("active_") ||
                                      key.Contains("top_priority_") ||
                                      key.Contains("brand_") ||
                                      key.Contains("type_") ||
                                      key.Contains("price_") ||
                                      key.Contains("city_") ||
                                      key.Contains("search_")
                                  ))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _cache.Remove(key);
                    _trackedCacheKeys.TryRemove(key, out _);
                }
            }
            finally
            {
                _trackedKeysLock.Release();
            }
        }

        /// <summary>
        /// Adds a cache key to tracking and sets the cache entry
        /// </summary>
        private void SetCacheEntry<T>(string key, T value, MemoryCacheEntryOptions options)
        {
            _cache.Set(key, value, options);
            _trackedCacheKeys.TryAdd(key, 0);
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
                var fullUrl = $"{BASE_PATH}{queryString}";

                _logger.LogInformation("Calling FeaturedVehicle API: {Url}", fullUrl);

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync(fullUrl)
                );

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get featured vehicles. Status: {StatusCode}", response.StatusCode);
                    return new PagedResult<FeaturedVehicleResponseDto>();
                }

                var result = await ReadApiResponseAsync<PagedResult<FeaturedVehicleResponseDto>>(response)
                             ?? new PagedResult<FeaturedVehicleResponseDto>();

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                    SlidingExpiration = TimeSpan.FromMinutes(2),
                    Priority = CacheItemPriority.Normal
                };

                SetCacheEntry(cacheKey, result, cacheOptions);

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

            var fullUrl = $"{BASE_PATH}/{Uri.EscapeDataString(id)}";
            _logger.LogInformation("Calling FeaturedVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get featured vehicle by ID {Id}. Status: {StatusCode}", id, response.StatusCode);
                return null;
            }

            var item = await ReadApiResponseAsync<FeaturedVehicleResponseDto>(response);

            if (item != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(3),
                    Priority = CacheItemPriority.High
                };

                SetCacheEntry(cacheKey, item, cacheOptions);
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

            var fullUrl = $"{BASE_PATH}/slug/{Uri.EscapeDataString(slug)}";
            _logger.LogInformation("Calling FeaturedVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get featured vehicle by slug {Slug}. Status: {StatusCode}", slug, response.StatusCode);
                return null;
            }

            var item = await ReadApiResponseAsync<FeaturedVehicleResponseDto>(response);

            if (item != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(3)
                };

                SetCacheEntry(cacheKey, item, cacheOptions);
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

            var fullUrl = $"{BASE_PATH}/model/{Uri.EscapeDataString(modelSlug)}";
            _logger.LogInformation("Calling FeaturedVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get featured vehicle by model slug {ModelSlug}. Status: {StatusCode}", modelSlug, response.StatusCode);
                return null;
            }

            var item = await ReadApiResponseAsync<FeaturedVehicleResponseDto>(response);

            if (item != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(3)
                };

                SetCacheEntry(cacheKey, item, cacheOptions);
            }

            return item;
        }

        // ── CRUD Operations with Cache Invalidation ─────────────────────────────────

        public async Task<FeaturedVehicleResponseDto> CreateAsync(FeaturedVehicleRequestDto request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            _logger.LogInformation("Creating new featured vehicle");

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync(BASE_PATH, request)
            );

            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new InvalidOperationException("A featured vehicle with this model already exists");

            response.EnsureSuccessStatusCode();

            var result = await ReadApiResponseAsync<FeaturedVehicleResponseDto>(response)
                         ?? throw new Exception("Failed to create featured vehicle: Invalid response from API");

            // Invalidate all caches on create
            await InvalidateAllCachesAsync();

            return result;
        }

        public async Task<FeaturedVehicleResponseDto?> UpdateAsync(string id, FeaturedVehicleRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            _logger.LogInformation("Updating featured vehicle {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PutAsJsonAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}", request)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to update featured vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
                return null;
            }

            var result = await ReadApiResponseAsync<FeaturedVehicleResponseDto>(response);

            if (result != null)
            {
                // Invalidate specific vehicle caches
                await InvalidateVehicleCachesAsync(id);
                // Also invalidate all list caches since ordering might change
                await InvalidateAllCachesAsync();
            }

            return result;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            _logger.LogInformation("Deleting featured vehicle {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.DeleteAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}")
            );

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to delete featured vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
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

            _logger.LogInformation("Updating priority for featured vehicle {Id} to {Priority}", id, priority);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PatchAsJsonAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}/priority", request)
            );

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to update priority for featured vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        public async Task<bool> BulkUpdatePriorityAsync(Dictionary<string, int> priorityUpdates)
        {
            if (priorityUpdates == null || priorityUpdates.Count == 0)
                return false;

            _logger.LogInformation("Bulk updating priorities for {Count} vehicles", priorityUpdates.Count);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync($"{BASE_PATH}/bulk/priority", priorityUpdates)
            );

            if (response.IsSuccessStatusCode)
            {
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to bulk update priorities. Status: {StatusCode}", response.StatusCode);
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

            _logger.LogInformation("Activating featured vehicle {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}/activate", content)
            );

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to activate featured vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        public async Task<bool> DeactivateAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            _logger.LogInformation("Deactivating featured vehicle {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}/deactivate", null)
            );

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to deactivate featured vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        // ── Filtered Collections (with shorter cache durations) ─────────────────

        public async Task<IEnumerable<FeaturedVehicleSummaryDto>> GetActiveFeaturedVehiclesAsync(int limit = 50)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}active_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeaturedVehicleSummaryDto>? cached))
                return cached!;

            var fullUrl = $"{BASE_PATH}/active?limit={limit}";
            _logger.LogInformation("Calling FeaturedVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get active featured vehicles. Status: {StatusCode}", response.StatusCode);
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<FeaturedVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<FeaturedVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);

            return items;
        }

        public async Task<IEnumerable<FeaturedVehicleSummaryDto>> GetTopPriorityFeaturedVehiclesAsync(int limit = 10)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}top_priority_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeaturedVehicleSummaryDto>? cached))
                return cached!;

            var fullUrl = $"{BASE_PATH}/top-priority?limit={limit}";
            _logger.LogInformation("Calling FeaturedVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get top priority featured vehicles. Status: {StatusCode}", response.StatusCode);
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<FeaturedVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<FeaturedVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);

            return items;
        }

        // ── Engagement Operations (with selective cache invalidation) ─────────────────

        public async Task<bool> IncrementViewsAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}/view", null)
            );

            // Views don't typically require cache invalidation
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
                await InvalidateVehicleCachesAsync(id);
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
                await InvalidateVehicleCachesAsync(id);
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
                await InvalidateVehicleCachesAsync(id);
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
                await InvalidateVehicleCachesAsync(id);
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

            var fullUrl = $"{BASE_PATH}/brand/{Uri.EscapeDataString(brandName)}?limit={limit}";
            _logger.LogInformation("Calling FeaturedVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get featured vehicles by brand {BrandName}. Status: {StatusCode}", brandName, response.StatusCode);
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<FeaturedVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<FeaturedVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);

            return items;
        }

        public async Task<IEnumerable<FeaturedVehicleSummaryDto>> GetByVehicleTypeAsync(string vehicleType, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(vehicleType))
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}type_{vehicleType}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeaturedVehicleSummaryDto>? cached))
                return cached!;

            var fullUrl = $"{BASE_PATH}/type/{Uri.EscapeDataString(vehicleType)}?limit={limit}";
            _logger.LogInformation("Calling FeaturedVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get featured vehicles by type {VehicleType}. Status: {StatusCode}", vehicleType, response.StatusCode);
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<FeaturedVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<FeaturedVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);

            return items;
        }

        public async Task<IEnumerable<FeaturedVehicleSummaryDto>> GetByPriceRangeAsync(decimal minPrice, decimal maxPrice, int limit = 20)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}price_{minPrice}_{maxPrice}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeaturedVehicleSummaryDto>? cached))
                return cached!;

            var fullUrl = $"{BASE_PATH}/price-range?minPrice={minPrice}&maxPrice={maxPrice}&limit={limit}";
            _logger.LogInformation("Calling FeaturedVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get featured vehicles by price range {MinPrice}-{MaxPrice}. Status: {StatusCode}", minPrice, maxPrice, response.StatusCode);
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<FeaturedVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<FeaturedVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);

            return items;
        }

        public async Task<IEnumerable<FeaturedVehicleSummaryDto>> GetByCityAsync(string city, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(city))
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}city_{city}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeaturedVehicleSummaryDto>? cached))
                return cached!;

            var fullUrl = $"{BASE_PATH}/city/{Uri.EscapeDataString(city)}?limit={limit}";
            _logger.LogInformation("Calling FeaturedVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get featured vehicles by city {City}. Status: {StatusCode}", city, response.StatusCode);
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<FeaturedVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<FeaturedVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);

            return items;
        }

        public async Task<IEnumerable<FeaturedVehicleSummaryDto>> SearchAsync(string searchTerm, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}search_{searchTerm}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeaturedVehicleSummaryDto>? cached))
                return cached!;

            var fullUrl = $"{BASE_PATH}/search?q={Uri.EscapeDataString(searchTerm)}&limit={limit}";
            _logger.LogInformation("Calling FeaturedVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to search featured vehicles with term {SearchTerm}. Status: {StatusCode}", searchTerm, response.StatusCode);
                return Enumerable.Empty<FeaturedVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<FeaturedVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<FeaturedVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);

            return items;
        }
    }
}