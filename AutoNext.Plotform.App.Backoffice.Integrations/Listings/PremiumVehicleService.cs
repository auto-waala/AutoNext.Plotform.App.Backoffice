using AutoNext.Plotform.App.Backoffice.Models.Common;
using AutoNext.Plotform.App.Backoffice.Models.Listings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using System.Net;
using System.Net.Http.Json;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Listings
{
    public class PremiumVehicleService : IPremiumVehicleService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PremiumVehicleService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);
        private const string CACHE_KEY_PREFIX = "premium_vehicle_";

        public PremiumVehicleService(
            HttpClient httpClient,
            IMemoryCache memoryCache,
            ILogger<PremiumVehicleService> logger)
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
            _logger.LogDebug("Invalidating all premium vehicle caches");
        }

        private void InvalidateVehicleCache(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            _cache.Remove($"{CACHE_KEY_PREFIX}{id}");
            _cache.Remove($"{CACHE_KEY_PREFIX}slug_*");
            _cache.Remove($"{CACHE_KEY_PREFIX}model_slug_*");
            _logger.LogDebug("Invalidated cache for vehicle: {Id}", id);
        }

        private string GetPagedCacheKey(int page, int pageSize, string? sortBy, string? sortOrder) =>
            $"{CACHE_KEY_PREFIX}all_{page}_{pageSize}_{sortBy ?? "none"}_{sortOrder ?? "none"}";

        public async Task<PagedResult<PremiumVehicleResponseDto>> GetAllAsync(int page, int pageSize, string? sortBy = null, string? sortOrder = null)
        {
            string cacheKey = GetPagedCacheKey(page, pageSize, sortBy, sortOrder);

            if (_cache.TryGetValue(cacheKey, out PagedResult<PremiumVehicleResponseDto>? cached))
                return cached!;

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cached))
                    return cached!;

                var queryParams = $"page={page}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(sortBy))
                    queryParams += $"&sortBy={sortBy}";
                if (!string.IsNullOrEmpty(sortOrder))
                    queryParams += $"&sortOrder={sortOrder}";

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/premiumvehicle?{queryParams}")
                );

                if (!response.IsSuccessStatusCode)
                    return new PagedResult<PremiumVehicleResponseDto>();

                var result = await ReadApiResponseAsync<PagedResult<PremiumVehicleResponseDto>>(response)
                             ?? new PagedResult<PremiumVehicleResponseDto>();

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

        public async Task<PremiumVehicleResponseDto?> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            string cacheKey = $"{CACHE_KEY_PREFIX}{id}";

            if (_cache.TryGetValue(cacheKey, out PremiumVehicleResponseDto? cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/premiumvehicle/{id}")
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var item = await ReadApiResponseAsync<PremiumVehicleResponseDto>(response);

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

        public async Task<PremiumVehicleResponseDto?> GetBySlugAsync(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return null;

            string cacheKey = $"{CACHE_KEY_PREFIX}slug_{slug}";

            if (_cache.TryGetValue(cacheKey, out PremiumVehicleResponseDto? cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/premiumvehicle/slug/{slug}")
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var item = await ReadApiResponseAsync<PremiumVehicleResponseDto>(response);

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

        public async Task<PremiumVehicleResponseDto?> GetByModelSlugAsync(string modelSlug)
        {
            if (string.IsNullOrWhiteSpace(modelSlug))
                return null;

            string cacheKey = $"{CACHE_KEY_PREFIX}model_slug_{modelSlug}";

            if (_cache.TryGetValue(cacheKey, out PremiumVehicleResponseDto? cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/premiumvehicle/modelslug/{modelSlug}")
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var item = await ReadApiResponseAsync<PremiumVehicleResponseDto>(response);

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

        public async Task<IEnumerable<PremiumVehicleSummaryDto>> GetActivePremiumVehiclesAsync(int limit = 50)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}active_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<PremiumVehicleSummaryDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/premiumvehicle/active?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<PremiumVehicleSummaryDto>();

            var items = await ReadApiResponseAsync<IEnumerable<PremiumVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<PremiumVehicleSummaryDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            });

            return items;
        }

        public async Task<IEnumerable<PremiumVehicleSummaryDto>> GetTopPriorityPremiumVehiclesAsync(int limit = 10)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}toppriority_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<PremiumVehicleSummaryDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/premiumvehicle/toppriority?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<PremiumVehicleSummaryDto>();

            var items = await ReadApiResponseAsync<IEnumerable<PremiumVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<PremiumVehicleSummaryDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            });

            return items;
        }

        public async Task<IEnumerable<PremiumVehicleSummaryDto>> GetByBrandAsync(string brandName, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(brandName))
                return Enumerable.Empty<PremiumVehicleSummaryDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}brand_{brandName}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<PremiumVehicleSummaryDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/premiumvehicle/brand/{Uri.EscapeDataString(brandName)}?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<PremiumVehicleSummaryDto>();

            var items = await ReadApiResponseAsync<IEnumerable<PremiumVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<PremiumVehicleSummaryDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<PremiumVehicleSummaryDto>> GetByCityAsync(string city, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(city))
                return Enumerable.Empty<PremiumVehicleSummaryDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}city_{city}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<PremiumVehicleSummaryDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/premiumvehicle/city/{Uri.EscapeDataString(city)}?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<PremiumVehicleSummaryDto>();

            var items = await ReadApiResponseAsync<IEnumerable<PremiumVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<PremiumVehicleSummaryDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<PremiumVehicleSummaryDto>> GetByVehicleTypeAsync(string vehicleType, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(vehicleType))
                return Enumerable.Empty<PremiumVehicleSummaryDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}vehicletype_{vehicleType}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<PremiumVehicleSummaryDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/premiumvehicle/vehicletype/{Uri.EscapeDataString(vehicleType)}?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<PremiumVehicleSummaryDto>();

            var items = await ReadApiResponseAsync<IEnumerable<PremiumVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<PremiumVehicleSummaryDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<PremiumVehicleSummaryDto>> GetByPriceRangeAsync(decimal minPrice, decimal maxPrice, int limit = 20)
        {
            string cacheKey = $"{CACHE_KEY_PREFIX}pricerange_{minPrice}_{maxPrice}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<PremiumVehicleSummaryDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/premiumvehicle/pricerange?minPrice={minPrice}&maxPrice={maxPrice}&limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<PremiumVehicleSummaryDto>();

            var items = await ReadApiResponseAsync<IEnumerable<PremiumVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<PremiumVehicleSummaryDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return items;
        }

        public async Task<IEnumerable<PremiumVehicleSummaryDto>> SearchAsync(string searchTerm, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return Enumerable.Empty<PremiumVehicleSummaryDto>();

            string cacheKey = $"{CACHE_KEY_PREFIX}search_{searchTerm}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<PremiumVehicleSummaryDto>? cached))
                return cached!;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/premiumvehicle/search?term={Uri.EscapeDataString(searchTerm)}&limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<PremiumVehicleSummaryDto>();

            var items = await ReadApiResponseAsync<IEnumerable<PremiumVehicleSummaryDto>>(response)
                        ?? Enumerable.Empty<PremiumVehicleSummaryDto>();

            _cache.Set(cacheKey, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            });

            return items;
        }

        public async Task<PremiumVehicleResponseDto> CreateAsync(PremiumVehicleRequestDto request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync("api/v1/premiumvehicle", request)
            );

            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new InvalidOperationException("Duplicate entry - Slug or ModelSlug already exists");

            response.EnsureSuccessStatusCode();

            var result = await ReadApiResponseAsync<PremiumVehicleResponseDto>(response)
                         ?? throw new Exception("Invalid response");

            InvalidateAllCaches();

            return result;
        }

        public async Task<PremiumVehicleResponseDto?> UpdateAsync(string id, PremiumVehicleRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PutAsJsonAsync($"api/v1/premiumvehicle/{id}", request)
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var result = await ReadApiResponseAsync<PremiumVehicleResponseDto>(response);

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
                _httpClient.DeleteAsync($"api/v1/premiumvehicle/{id}")
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            return false;
        }

        public async Task<bool> ActivateAsync(string id, DateTime? endDate = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var request = new { endDate };
            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync($"api/v1/premiumvehicle/{id}/activate", request)
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
                _httpClient.PostAsync($"api/v1/premiumvehicle/{id}/deactivate", null)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            return false;
        }

        public async Task<bool> UpdatePriorityAsync(string id, int priority)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var request = new { priority };
            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PatchAsJsonAsync($"api/v1/premiumvehicle/{id}/priority", request)
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
            if (priorityUpdates == null || !priorityUpdates.Any())
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PatchAsJsonAsync("api/v1/premiumvehicle/bulk-priority", priorityUpdates)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateAllCaches();
                return true;
            }

            return false;
        }

        public async Task<bool> AddRatingAsync(string id, PremiumVehicleUserRatingDto rating)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            if (rating == null)
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync($"api/v1/premiumvehicle/{id}/ratings", rating)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                return true;
            }

            return false;
        }

        public async Task<bool> IncrementViewsAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsync($"api/v1/premiumvehicle/{id}/increment-views", null)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                return true;
            }

            return false;
        }

        public async Task<bool> IncrementLikesAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsync($"api/v1/premiumvehicle/{id}/increment-likes", null)
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
                _httpClient.PostAsync($"api/v1/premiumvehicle/{id}/increment-shares", null)
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
                _httpClient.PostAsync($"api/v1/premiumvehicle/{id}/increment-enquiries", null)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                return true;
            }

            return false;
        }

        public async Task<bool> IsSlugUniqueAsync(string slug, string? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return false;

            var queryParams = $"slug={Uri.EscapeDataString(slug)}";
            if (!string.IsNullOrEmpty(excludeId))
                queryParams += $"&excludeId={excludeId}";

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/premiumvehicle/is-slug-unique?{queryParams}")
            );

            if (!response.IsSuccessStatusCode)
                return false;

            var result = await ReadApiResponseAsync<bool>(response);
            return result;
        }

        public async Task<bool> IsModelSlugUniqueAsync(string modelSlug, string? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(modelSlug))
                return false;

            var queryParams = $"modelSlug={Uri.EscapeDataString(modelSlug)}";
            if (!string.IsNullOrEmpty(excludeId))
                queryParams += $"&excludeId={excludeId}";

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/premiumvehicle/is-model-slug-unique?{queryParams}")
            );

            if (!response.IsSuccessStatusCode)
                return false;

            var result = await ReadApiResponseAsync<bool>(response);
            return result;
        }
    }
}