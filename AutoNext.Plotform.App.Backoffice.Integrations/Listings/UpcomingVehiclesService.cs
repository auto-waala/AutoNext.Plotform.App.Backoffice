using AutoNext.Plotform.App.Backoffice.Models.Common;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Listings
{
    public class UpcomingVehiclesService : IUpcomingVehiclesService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<UpcomingVehiclesService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        // Track all cache keys for this service instance
        private readonly ConcurrentDictionary<string, byte> _trackedCacheKeys = new ConcurrentDictionary<string, byte>();
        private readonly SemaphoreSlim _trackedKeysLock = new SemaphoreSlim(1, 1);

        private const string BASE_PATH = "api/v1/upcomingvehicle";
        private const string CACHE_KEY_PREFIX = "upcoming_vehicle_";

        public UpcomingVehiclesService(
            HttpClient httpClient,
            IMemoryCache memoryCache,
            ILogger<UpcomingVehiclesService> logger)
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

        private async Task InvalidateAllCachesAsync()
        {
            _logger.LogInformation("Invalidating all upcoming vehicle caches. Total tracked keys: {Count}", _trackedCacheKeys.Count);

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

        private async Task InvalidateVehicleCachesAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            _logger.LogDebug("Invalidating all caches for upcoming vehicle: {Id}", id);

            await _trackedKeysLock.WaitAsync();
            try
            {
                var keysToRemove = _trackedCacheKeys.Keys
                    .Where(key => key.Contains(id) ||
                                  key.StartsWith(CACHE_KEY_PREFIX) && (
                                      key.Contains("all_") ||
                                      key.Contains("active_") ||
                                      key.Contains("featured_") ||
                                      key.Contains("latest_") ||
                                      key.Contains("launches_") ||
                                      key.Contains("brand_") ||
                                      key.Contains("type_") ||
                                      key.Contains("pricerange_") ||
                                      key.Contains("launchperiod_") ||
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

        private void SetCacheEntry<T>(string key, T value, MemoryCacheEntryOptions options)
        {
            _cache.Set(key, value, options);
            _trackedCacheKeys.TryAdd(key, 0);
        }

        private string GetPagedCacheKey(int page, int pageSize, string? sortBy, string? sortOrder) =>
            $"{CACHE_KEY_PREFIX}all_{page}_{pageSize}_{sortBy ?? "none"}_{sortOrder ?? "none"}";

        // GET Endpoints implementations

        public async Task<PagedResult<UpcomingVehicleResponseDto>> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null)
        {
            string cacheKey = GetPagedCacheKey(page, pageSize, sortBy, sortOrder);

            if (_cache.TryGetValue(cacheKey, out PagedResult<UpcomingVehicleResponseDto>? cached))
                return cached!;

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cached))
                    return cached!;

                var queryParams = new List<string> { $"page={page}", $"pageSize={pageSize}" };
                if (!string.IsNullOrEmpty(sortBy))
                    queryParams.Add($"sortBy={Uri.EscapeDataString(sortBy)}");
                if (!string.IsNullOrEmpty(sortOrder))
                    queryParams.Add($"sortOrder={Uri.EscapeDataString(sortOrder)}");

                var fullUrl = $"{BASE_PATH}?{string.Join("&", queryParams)}";
                _logger.LogInformation("Calling UpcomingVehicle API: {Url}", fullUrl);

                var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(fullUrl));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get upcoming vehicles. Status: {StatusCode}", response.StatusCode);
                    return new PagedResult<UpcomingVehicleResponseDto>();
                }

                var result = await ReadApiResponseAsync<PagedResult<UpcomingVehicleResponseDto>>(response)
                             ?? new PagedResult<UpcomingVehicleResponseDto>();

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

        public async Task<IEnumerable<UpcomingVehicleSummaryDto>> GetActiveUpcomingVehiclesAsync(int limit = 50)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}active_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UpcomingVehicleSummaryDto>? cached))
                return cached!;

            var fullUrl = $"{BASE_PATH}/active?limit={limit}";
            _logger.LogInformation("Calling UpcomingVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(fullUrl));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get active upcoming vehicles. Status: {StatusCode}", response.StatusCode);
                return Enumerable.Empty<UpcomingVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<UpcomingVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<UpcomingVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);
            return items;
        }

        public async Task<IEnumerable<UpcomingVehicleSummaryDto>> GetFeaturedUpcomingVehiclesAsync(int limit = 10)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}featured_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UpcomingVehicleSummaryDto>? cached))
                return cached!;

            var fullUrl = $"{BASE_PATH}/featured?limit={limit}";
            _logger.LogInformation("Calling UpcomingVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(fullUrl));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get featured upcoming vehicles. Status: {StatusCode}", response.StatusCode);
                return Enumerable.Empty<UpcomingVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<UpcomingVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<UpcomingVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);
            return items;
        }

        public async Task<IEnumerable<UpcomingVehicleSummaryDto>> GetLatestUpcomingVehiclesAsync(int limit = 20)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}latest_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UpcomingVehicleSummaryDto>? cached))
                return cached!;

            var fullUrl = $"{BASE_PATH}/latest?limit={limit}";
            _logger.LogInformation("Calling UpcomingVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(fullUrl));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get latest upcoming vehicles. Status: {StatusCode}", response.StatusCode);
                return Enumerable.Empty<UpcomingVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<UpcomingVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<UpcomingVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);
            return items;
        }

        public async Task<IEnumerable<UpcomingVehicleSummaryDto>> GetUpcomingLaunchesAsync(int days = 30)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}launches_{days}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UpcomingVehicleSummaryDto>? cached))
                return cached!;

            var fullUrl = $"{BASE_PATH}/upcoming-launches?days={days}";
            _logger.LogInformation("Calling UpcomingVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(fullUrl));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get upcoming launches. Status: {StatusCode}", response.StatusCode);
                return Enumerable.Empty<UpcomingVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<UpcomingVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<UpcomingVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);
            return items;
        }

        public async Task<UpcomingVehicleResponseDto?> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            string cacheKey = $"{CACHE_KEY_PREFIX}{id}";

            if (_cache.TryGetValue(cacheKey, out UpcomingVehicleResponseDto? cached))
                return cached;

            var fullUrl = $"{BASE_PATH}/{Uri.EscapeDataString(id)}";
            _logger.LogInformation("Calling UpcomingVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(fullUrl));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get upcoming vehicle by ID {Id}. Status: {StatusCode}", id, response.StatusCode);
                return null;
            }

            var item = await ReadApiResponseAsync<UpcomingVehicleResponseDto>(response);

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

        public async Task<UpcomingVehicleResponseDto?> GetBySlugAsync(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return null;

            string cacheKey = $"{CACHE_KEY_PREFIX}slug_{slug}";

            if (_cache.TryGetValue(cacheKey, out UpcomingVehicleResponseDto? cached))
                return cached;

            var fullUrl = $"{BASE_PATH}/slug/{Uri.EscapeDataString(slug)}";
            _logger.LogInformation("Calling UpcomingVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(fullUrl));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get upcoming vehicle by slug {Slug}. Status: {StatusCode}", slug, response.StatusCode);
                return null;
            }

            var item = await ReadApiResponseAsync<UpcomingVehicleResponseDto>(response);

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

        public async Task<UpcomingVehicleResponseDto?> GetByModelSlugAsync(string modelSlug)
        {
            if (string.IsNullOrWhiteSpace(modelSlug)) return null;

            string cacheKey = $"{CACHE_KEY_PREFIX}model_slug_{modelSlug}";

            if (_cache.TryGetValue(cacheKey, out UpcomingVehicleResponseDto? cached))
                return cached;

            var fullUrl = $"{BASE_PATH}/model/{Uri.EscapeDataString(modelSlug)}"; // Matches controller route `model/{modelSlug}`
            _logger.LogInformation("Calling UpcomingVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(fullUrl));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get upcoming vehicle by model slug {ModelSlug}. Status: {StatusCode}", modelSlug, response.StatusCode);
                return null;
            }

            var item = await ReadApiResponseAsync<UpcomingVehicleResponseDto>(response);

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

        // Filter Implementations

        public async Task<IEnumerable<UpcomingVehicleSummaryDto>> GetByBrandAsync(string brandName, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(brandName)) return Enumerable.Empty<UpcomingVehicleSummaryDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}brand_{brandName}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UpcomingVehicleSummaryDto>? cached))
                return cached!;

            var fullUrl = $"{BASE_PATH}/brand/{Uri.EscapeDataString(brandName)}?limit={limit}";
            _logger.LogInformation("Calling UpcomingVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(fullUrl));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get upcoming vehicles by brand {BrandName}. Status: {StatusCode}", brandName, response.StatusCode);
                return Enumerable.Empty<UpcomingVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<UpcomingVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<UpcomingVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);
            return items;
        }

        public async Task<IEnumerable<UpcomingVehicleSummaryDto>> GetByVehicleTypeAsync(string vehicleType, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(vehicleType)) return Enumerable.Empty<UpcomingVehicleSummaryDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}type_{vehicleType}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UpcomingVehicleSummaryDto>? cached))
                return cached!;

            var fullUrl = $"{BASE_PATH}/type/{Uri.EscapeDataString(vehicleType)}?limit={limit}";
            _logger.LogInformation("Calling UpcomingVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(fullUrl));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get upcoming vehicles by type {VehicleType}. Status: {StatusCode}", vehicleType, response.StatusCode);
                return Enumerable.Empty<UpcomingVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<UpcomingVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<UpcomingVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);
            return items;
        }

        public async Task<IEnumerable<UpcomingVehicleSummaryDto>> GetByPriceRangeAsync(decimal minPrice, decimal maxPrice, int limit = 20)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}pricerange_{minPrice}_{maxPrice}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UpcomingVehicleSummaryDto>? cached))
                return cached!;

            var fullUrl = $"{BASE_PATH}/price-range?minPrice={minPrice}&maxPrice={maxPrice}&limit={limit}";
            _logger.LogInformation("Calling UpcomingVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(fullUrl));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get upcoming vehicles by price range {MinPrice}-{MaxPrice}. Status: {StatusCode}", minPrice, maxPrice, response.StatusCode);
                return Enumerable.Empty<UpcomingVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<UpcomingVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<UpcomingVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);
            return items;
        }

        public async Task<IEnumerable<UpcomingVehicleSummaryDto>> GetByLaunchPeriodAsync(string launchPeriod, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(launchPeriod)) return Enumerable.Empty<UpcomingVehicleSummaryDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}launchperiod_{launchPeriod}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UpcomingVehicleSummaryDto>? cached))
                return cached!;

            var fullUrl = $"{BASE_PATH}/launch-period/{Uri.EscapeDataString(launchPeriod)}?limit={limit}";
            _logger.LogInformation("Calling UpcomingVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(fullUrl));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get upcoming vehicles by launch period {LaunchPeriod}. Status: {StatusCode}", launchPeriod, response.StatusCode);
                return Enumerable.Empty<UpcomingVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<UpcomingVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<UpcomingVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);
            return items;
        }

        public async Task<IEnumerable<UpcomingVehicleSummaryDto>> SearchAsync(string searchTerm, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(searchTerm)) return Enumerable.Empty<UpcomingVehicleSummaryDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}search_{searchTerm}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UpcomingVehicleSummaryDto>? cached))
                return cached!;

            var fullUrl = $"{BASE_PATH}/search?q={Uri.EscapeDataString(searchTerm)}&limit={limit}";
            _logger.LogInformation("Calling UpcomingVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(fullUrl));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to search upcoming vehicles with term {SearchTerm}. Status: {StatusCode}", searchTerm, response.StatusCode);
                return Enumerable.Empty<UpcomingVehicleSummaryDto>();
            }

            var items = await ReadApiResponseAsync<IEnumerable<UpcomingVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<UpcomingVehicleSummaryDto>();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            };

            SetCacheEntry(cacheKey, items, cacheOptions);
            return items;
        }

        // CRUD Implementations

        public async Task<UpcomingVehicleResponseDto> CreateAsync(UpcomingVehicleRequestDto request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            _logger.LogInformation("Creating new upcoming vehicle for brand: {BrandName}, model: {ModelName}", request.BrandName, request.ModelName);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsJsonAsync(BASE_PATH, request));

            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new InvalidOperationException("Duplicate entry - Slug or ModelSlug already exists");

            response.EnsureSuccessStatusCode();

            var result = await ReadApiResponseAsync<UpcomingVehicleResponseDto>(response)
                         ?? throw new Exception("Invalid response from API");

            await InvalidateAllCachesAsync();
            return result;
        }

        public async Task<UpcomingVehicleResponseDto?> UpdateAsync(string id, UpcomingVehicleRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            if (request == null) throw new ArgumentNullException(nameof(request));

            _logger.LogInformation("Updating upcoming vehicle {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PutAsJsonAsync($"{BASE_PATH}/{id}", request));

            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new InvalidOperationException("Duplicate entry or validation mismatch status");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to update upcoming vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
                return null;
            }

            var result = await ReadApiResponseAsync<UpcomingVehicleResponseDto>(response);

            if (result != null)
            {
                await InvalidateVehicleCachesAsync(id);
                await InvalidateAllCachesAsync();
            }

            return result;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            _logger.LogInformation("Deleting upcoming vehicle {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.DeleteAsync($"{BASE_PATH}/{id}"));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to delete upcoming vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        // Feature & Marketing Status Operations

        public async Task<bool> MarkAsFeaturedAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            _logger.LogInformation("Marking upcoming vehicle as featured: {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync($"{BASE_PATH}/{id}/feature", null));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to mark upcoming vehicle as featured {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        public async Task<bool> RemoveFeaturedAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            _logger.LogInformation("Removing featured status from upcoming vehicle: {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.DeleteAsync($"{BASE_PATH}/{id}/feature"));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to remove featured status from upcoming vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        public async Task<bool> UpdatePriorityAsync(string id, int priority)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            _logger.LogInformation("Updating priority for upcoming vehicle {Id} to {Priority}", id, priority);

            var request = new { priority };
            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PatchAsJsonAsync($"{BASE_PATH}/{id}/priority", request));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to update priority for upcoming vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        public async Task<bool> MarkAsLaunchedAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            _logger.LogInformation("Marking upcoming vehicle as launched: {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync($"{BASE_PATH}/{id}/launch", null));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to mark upcoming vehicle as launched {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        // Status Activations

        public async Task<bool> ActivateAsync(string id, DateTime? launchDate = null)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            _logger.LogInformation("Activating upcoming vehicle {Id} with launch date {LaunchDate}", id, launchDate);

            var request = new { launchDate };
            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsJsonAsync($"{BASE_PATH}/{id}/activate", request));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to activate upcoming vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        public async Task<bool> DeactivateAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            _logger.LogInformation("Deactivating upcoming vehicle {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync($"{BASE_PATH}/{id}/deactivate", null));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to deactivate upcoming vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        // Bulk Operation Implementations

        public async Task<bool> BulkUpdatePriorityAsync(Dictionary<string, int> priorityUpdates)
        {
            if (priorityUpdates == null || !priorityUpdates.Any()) return false;

            _logger.LogInformation("Bulk updating priorities for {Count} vehicles", priorityUpdates.Count);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsJsonAsync($"{BASE_PATH}/bulk/priority", priorityUpdates));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to bulk update priorities. Status: {StatusCode}", response.StatusCode);
            return false;
        }

        public async Task<bool> BulkUpdateFeaturedAsync(Dictionary<string, bool> featuredUpdates)
        {
            if (featuredUpdates == null || !featuredUpdates.Any()) return false;

            _logger.LogInformation("Bulk updating featured status for {Count} vehicles", featuredUpdates.Count);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsJsonAsync($"{BASE_PATH}/bulk/featured", featuredUpdates));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to bulk update featured statuses. Status: {StatusCode}", response.StatusCode);
            return false;
        }

        public async Task<bool> BulkActivateAsync(List<string> ids, DateTime? launchDate = null)
        {
            if (ids == null || !ids.Any()) return false;

            _logger.LogInformation("Bulk activating {Count} upcoming vehicles", ids.Count);

            var request = new { vehicleIds = ids, launchDate };
            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsJsonAsync($"{BASE_PATH}/bulk/activate", request));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to bulk activate vehicles. Status: {StatusCode}", response.StatusCode);
            return false;
        }

        public async Task<bool> BulkDeactivateAsync(List<string> ids)
        {
            if (ids == null || !ids.Any()) return false;

            _logger.LogInformation("Bulk deactivating {Count} upcoming vehicles", ids.Count);

            var request = new { vehicleIds = ids };
            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsJsonAsync($"{BASE_PATH}/bulk/deactivate", request));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to bulk deactivate vehicles. Status: {StatusCode}", response.StatusCode);
            return false;
        }

        public async Task<bool> BulkMarkAsLaunchedAsync(List<string> ids)
        {
            if (ids == null || !ids.Any()) return false;

            _logger.LogInformation("Bulk marking {Count} upcoming vehicles as launched", ids.Count);

            var request = new { vehicleIds = ids };
            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsJsonAsync($"{BASE_PATH}/bulk/launch", request));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateAllCachesAsync();
                return true;
            }

            _logger.LogWarning("Failed to bulk launch vehicles. Status: {StatusCode}", response.StatusCode);
            return false;
        }


        // Engagement Metrics Implementations

        public async Task<bool> IncrementLikesAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync($"{BASE_PATH}/{id}/like", null));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                return true;
            }
            return false;
        }

        public async Task<bool> IncrementSharesAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync($"{BASE_PATH}/{id}/share", null));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                return true;
            }
            return false;
        }

        public async Task<bool> IncrementEnquiriesAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync($"{BASE_PATH}/{id}/enquire", null));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                return true;
            }
            return false;
        }

        public async Task<bool> IncrementViewsAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            // Handled as explicit POST path call behind scenes on controller via GetById routes
            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync($"{BASE_PATH}/{id}/increment-views", null));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                return true;
            }
            return false;
        }

        // Ratings Implementations

        public async Task<bool> AddRatingAsync(string id, UpcomingVehicleUserRatingDto rating)
        {
            if (string.IsNullOrWhiteSpace(id) || rating == null) return false;

            _logger.LogInformation("Adding rating {Rating} for upcoming vehicle: {Id}", rating.Rating, id);

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsJsonAsync($"{BASE_PATH}/{id}/rating", rating));

            if (response.IsSuccessStatusCode)
            {
                await InvalidateVehicleCachesAsync(id);
                return true;
            }

            _logger.LogWarning("Failed to add rating for upcoming vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        // Uniqueness Checks Implementations

        public async Task<bool> IsSlugUniqueAsync(string slug, string? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(slug)) return false;

            var queryParams = new List<string> { $"slug={Uri.EscapeDataString(slug)}" };
            if (!string.IsNullOrEmpty(excludeId))
                queryParams.Add($"excludeId={excludeId}");

            var fullUrl = $"{BASE_PATH}/is-slug-unique?{string.Join("&", queryParams)}";

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(fullUrl));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to check slug uniqueness. Status: {StatusCode}", response.StatusCode);
                return false;
            }

            return await ReadApiResponseAsync<bool>(response);
        }

        public async Task<bool> IsModelSlugUniqueAsync(string modelSlug, string? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(modelSlug)) return false;

            var queryParams = new List<string> { $"modelSlug={Uri.EscapeDataString(modelSlug)}" };
            if (!string.IsNullOrEmpty(excludeId))
                queryParams.Add($"excludeId={excludeId}");

            var fullUrl = $"{BASE_PATH}/is-model-slug-unique?{string.Join("&", queryParams)}";

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(fullUrl));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to check model slug uniqueness. Status: {StatusCode}", response.StatusCode);
                return false;
            }

            return await ReadApiResponseAsync<bool>(response);
        }
    }
}