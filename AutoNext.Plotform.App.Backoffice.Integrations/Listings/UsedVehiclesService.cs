using AutoNext.Plotform.App.Backoffice.Models.Common;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using System.Net;
using System.Net.Http.Json;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Listings
{
    public class UsedVehiclesService : IUsedVehiclesService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<UsedVehiclesService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);
        private const string BASE_PATH = "api/v1/UsedVehicles";
        private const string CACHE_KEY_PREFIX = "used_vehicle_";

        public UsedVehiclesService(
            HttpClient httpClient,
            IMemoryCache memoryCache,
            ILogger<UsedVehiclesService> logger)
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

        private void InvalidateAllCaches()
        {
            _logger.LogDebug("Invalidating all used vehicle caches");
            // Note: IMemoryCache doesn't support pattern removal directly.
            // For production, consider using IDistributedCache with Redis.
        }

        private void InvalidateVehicleCache(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            _cache.Remove($"{CACHE_KEY_PREFIX}{id}");
            _cache.Remove($"{CACHE_KEY_PREFIX}slug_*");
            _cache.Remove($"{CACHE_KEY_PREFIX}model_*");
            _logger.LogDebug("Invalidated cache for used vehicle: {Id}", id);
        }

        private string GetPagedCacheKey(int page, int pageSize, string? sortBy, string? sortOrder) =>
            $"{CACHE_KEY_PREFIX}all_{page}_{pageSize}_{sortBy ?? "none"}_{sortOrder ?? "none"}";

        // ── GET Single ──────────────────────────────────────────────────────────────

        public async Task<UsedVehiclesResponseDto?> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            string cacheKey = $"{CACHE_KEY_PREFIX}{id}";

            if (_cache.TryGetValue(cacheKey, out UsedVehiclesResponseDto? cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}")
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var item = await ReadApiResponseAsync<UsedVehiclesResponseDto>(response);

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

        public async Task<UsedVehiclesResponseDto?> GetBySlugAsync(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return null;

            string cacheKey = $"{CACHE_KEY_PREFIX}slug_{slug}";

            if (_cache.TryGetValue(cacheKey, out UsedVehiclesResponseDto? cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/slug/{Uri.EscapeDataString(slug)}")
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var item = await ReadApiResponseAsync<UsedVehiclesResponseDto>(response);

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

        public async Task<UsedVehiclesResponseDto?> GetByModelSlugAsync(string modelSlug)
        {
            if (string.IsNullOrWhiteSpace(modelSlug))
                return null;

            string cacheKey = $"{CACHE_KEY_PREFIX}model_{modelSlug}";

            if (_cache.TryGetValue(cacheKey, out UsedVehiclesResponseDto? cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/model/{Uri.EscapeDataString(modelSlug)}")
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var item = await ReadApiResponseAsync<UsedVehiclesResponseDto>(response);

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

        // ── GET Collections ─────────────────────────────────────────────────────────

        public async Task<PagedResult<UsedVehiclesResponseDto>> GetAllAsync(
            int page, int pageSize, string? sortBy = null, string? sortOrder = null)
        {
            string cacheKey = GetPagedCacheKey(page, pageSize, sortBy, sortOrder);

            if (_cache.TryGetValue(cacheKey, out PagedResult<UsedVehiclesResponseDto>? cached))
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
                    return new PagedResult<UsedVehiclesResponseDto>();

                var result = await ReadApiResponseAsync<PagedResult<UsedVehiclesResponseDto>>(response)
                             ?? new PagedResult<UsedVehiclesResponseDto>();

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

        public async Task<IEnumerable<UsedVehiclesResponseDto>> GetActiveUsedVehiclesAsync(int limit = 50)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}active_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UsedVehiclesResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/active?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<UsedVehiclesResponseDto>>(response)
                        ?? Enumerable.Empty<UsedVehiclesResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            });

            return items;
        }

        public async Task<IEnumerable<UsedVehiclesResponseDto>> GetTopPriorityUsedVehiclesAsync(int limit = 10)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}top_priority_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UsedVehiclesResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/top-priority?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<UsedVehiclesResponseDto>>(response)
                        ?? Enumerable.Empty<UsedVehiclesResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            });

            return items;
        }

        public async Task<IEnumerable<UsedVehiclesResponseDto>> GetRecentlyPostedAsync(int days = 7, int limit = 20)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}recent_{days}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UsedVehiclesResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/recent?days={days}&limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<UsedVehiclesResponseDto>>(response)
                        ?? Enumerable.Empty<UsedVehiclesResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            });

            return items;
        }

        // ── Filtered Collections ────────────────────────────────────────────────────

        public async Task<IEnumerable<UsedVehiclesResponseDto>> GetByBrandAsync(string brandName, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(brandName))
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}brand_{brandName}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UsedVehiclesResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/brand/{Uri.EscapeDataString(brandName)}?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<UsedVehiclesResponseDto>>(response)
                        ?? Enumerable.Empty<UsedVehiclesResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<UsedVehiclesResponseDto>> GetByVehicleTypeAsync(string vehicleType, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(vehicleType))
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}type_{vehicleType}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UsedVehiclesResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/type/{Uri.EscapeDataString(vehicleType)}?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<UsedVehiclesResponseDto>>(response)
                        ?? Enumerable.Empty<UsedVehiclesResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<UsedVehiclesResponseDto>> GetByPriceRangeAsync(decimal minPrice, decimal maxPrice, int limit = 20)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}price_{minPrice}_{maxPrice}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UsedVehiclesResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/price-range?minPrice={minPrice}&maxPrice={maxPrice}&limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<UsedVehiclesResponseDto>>(response)
                        ?? Enumerable.Empty<UsedVehiclesResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<UsedVehiclesResponseDto>> GetByCityAsync(string city, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(city))
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}city_{city}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UsedVehiclesResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/city/{Uri.EscapeDataString(city)}?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<UsedVehiclesResponseDto>>(response)
                        ?? Enumerable.Empty<UsedVehiclesResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<UsedVehiclesResponseDto>> GetByFuelTypeAsync(string fuelType, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(fuelType))
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}fuel_{fuelType}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UsedVehiclesResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/fuel-type/{Uri.EscapeDataString(fuelType)}?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<UsedVehiclesResponseDto>>(response)
                        ?? Enumerable.Empty<UsedVehiclesResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<UsedVehiclesResponseDto>> GetByTransmissionAsync(string transmission, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(transmission))
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}transmission_{transmission}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UsedVehiclesResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/transmission/{Uri.EscapeDataString(transmission)}?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<UsedVehiclesResponseDto>>(response)
                        ?? Enumerable.Empty<UsedVehiclesResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<UsedVehiclesResponseDto>> GetByYearRangeAsync(int minYear, int maxYear, int limit = 20)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}year_{minYear}_{maxYear}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UsedVehiclesResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/year-range?minYear={minYear}&maxYear={maxYear}&limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<UsedVehiclesResponseDto>>(response)
                        ?? Enumerable.Empty<UsedVehiclesResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<UsedVehiclesResponseDto>> GetBySellerTypeAsync(string sellerType, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(sellerType))
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}sellertype_{sellerType}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UsedVehiclesResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/seller-type/{Uri.EscapeDataString(sellerType)}?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<UsedVehiclesResponseDto>>(response)
                        ?? Enumerable.Empty<UsedVehiclesResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<UsedVehiclesResponseDto>> GetBySellerAsync(string sellerId, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(sellerId))
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}seller_{sellerId}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UsedVehiclesResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/seller/{Uri.EscapeDataString(sellerId)}?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<UsedVehiclesResponseDto>>(response)
                        ?? Enumerable.Empty<UsedVehiclesResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        // ── Search ──────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<UsedVehiclesResponseDto>> SearchAsync(string searchTerm, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}search_{searchTerm}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<UsedVehiclesResponseDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/search?q={Uri.EscapeDataString(searchTerm)}&limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<UsedVehiclesResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<UsedVehiclesResponseDto>>(response)
                        ?? Enumerable.Empty<UsedVehiclesResponseDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<PagedResult<UsedVehiclesResponseDto>> AdvancedSearchAsync(UsedVehiclesSearchCriteria criteria)
        {
            if (criteria == null)
                return new PagedResult<UsedVehiclesResponseDto>();

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync($"{BASE_PATH}/advanced-search", criteria)
            );

            if (!response.IsSuccessStatusCode)
                return new PagedResult<UsedVehiclesResponseDto>();

            return await ReadApiResponseAsync<PagedResult<UsedVehiclesResponseDto>>(response)
                   ?? new PagedResult<UsedVehiclesResponseDto>();
        }

        // ── CRUD Operations ─────────────────────────────────────────────────────────

        public async Task<UsedVehiclesResponseDto> CreateAsync(UsedVehiclesRequestDto request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync(BASE_PATH, request)
            );

            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new InvalidOperationException("A used vehicle with this slug already exists");

            response.EnsureSuccessStatusCode();

            var result = await ReadApiResponseAsync<UsedVehiclesResponseDto>(response)
                         ?? throw new Exception("Failed to create used vehicle: Invalid response from API");

            InvalidateAllCaches();

            return result;
        }

        public async Task<UsedVehiclesResponseDto?> UpdateAsync(string id, UsedVehiclesRequestDto request)
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

            var result = await ReadApiResponseAsync<UsedVehiclesResponseDto>(response);

            if (result != null)
            {
                InvalidateVehicleCache(id);
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

        // ── Used Vehicles Specific Operations ───────────────────────────────────────

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

        public async Task<bool> MarkAsSoldAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsync($"{BASE_PATH}/{Uri.EscapeDataString(id)}/mark-as-sold", null)
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

        // ── Engagement Operations ───────────────────────────────────────────────────

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

        // ── Rating ──────────────────────────────────────────────────────────────────

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

        // ── Validation ──────────────────────────────────────────────────────────────

        public async Task<bool> IsSlugUniqueAsync(string slug, string? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return false;

            var url = $"{BASE_PATH}/validate/slug/{Uri.EscapeDataString(slug)}";
            if (!string.IsNullOrWhiteSpace(excludeId))
                url += $"?excludeId={Uri.EscapeDataString(excludeId)}";

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(url)
            );

            if (!response.IsSuccessStatusCode)
                return false;

            return await ReadApiResponseAsync<bool>(response);
        }

        public async Task<bool> IsModelSlugUniqueAsync(string modelSlug, string? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(modelSlug))
                return false;

            var url = $"{BASE_PATH}/validate/model-slug/{Uri.EscapeDataString(modelSlug)}";
            if (!string.IsNullOrWhiteSpace(excludeId))
                url += $"?excludeId={Uri.EscapeDataString(excludeId)}";

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(url)
            );

            if (!response.IsSuccessStatusCode)
                return false;

            return await ReadApiResponseAsync<bool>(response);
        }

        // ── Statistics ──────────────────────────────────────────────────────────────

        public async Task<long> GetTotalCountAsync()
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}stats_total";

            if (_cache.TryGetValue(cacheKey, out long cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/statistics/counts")
            );

            if (!response.IsSuccessStatusCode)
                return 0;

            var stats = await ReadApiResponseAsync<VehicleStatisticsDto>(response);
            var total = stats?.Total ?? 0;

            _cache.Set(cacheKey, total, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return total;
        }

        public async Task<long> GetActiveCountAsync()
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}stats_active";

            if (_cache.TryGetValue(cacheKey, out long cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/statistics/counts")
            );

            if (!response.IsSuccessStatusCode)
                return 0;

            var stats = await ReadApiResponseAsync<VehicleStatisticsDto>(response);
            var active = stats?.Active ?? 0;

            _cache.Set(cacheKey, active, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return active;
        }

        public async Task<long> GetSoldCountAsync()
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}stats_sold";

            if (_cache.TryGetValue(cacheKey, out long cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"{BASE_PATH}/statistics/counts")
            );

            if (!response.IsSuccessStatusCode)
                return 0;

            var stats = await ReadApiResponseAsync<VehicleStatisticsDto>(response);
            var sold = stats?.Sold ?? 0;

            _cache.Set(cacheKey, sold, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return sold;
        }

        // ── Private DTOs for internal deserialization ───────────────────────────────

        private class VehicleStatisticsDto
        {
            public long Total { get; set; }
            public long Active { get; set; }
            public long Sold { get; set; }
        }
    }
}