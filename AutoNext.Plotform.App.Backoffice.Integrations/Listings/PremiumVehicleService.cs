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
        private const string BASE_PATH = "api/v1/premiumvehicle";
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

                var queryParams = new List<string> { $"page={page}", $"pageSize={pageSize}" };
                if (!string.IsNullOrEmpty(sortBy))
                    queryParams.Add($"sortBy={Uri.EscapeDataString(sortBy)}");
                if (!string.IsNullOrEmpty(sortOrder))
                    queryParams.Add($"sortOrder={Uri.EscapeDataString(sortOrder)}");

                var fullUrl = $"{BASE_PATH}?{string.Join("&", queryParams)}";
                _logger.LogInformation("Calling PremiumVehicle API: {Url}", fullUrl);

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync(fullUrl)
                );

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get premium vehicles. Status: {StatusCode}", response.StatusCode);
                    return new PagedResult<PremiumVehicleResponseDto>();
                }

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

            var fullUrl = $"{BASE_PATH}/{Uri.EscapeDataString(id)}";
            _logger.LogInformation("Calling PremiumVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get premium vehicle by ID {Id}. Status: {StatusCode}", id, response.StatusCode);
                return null;
            }

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

            var fullUrl = $"{BASE_PATH}/slug/{Uri.EscapeDataString(slug)}";
            _logger.LogInformation("Calling PremiumVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get premium vehicle by slug {Slug}. Status: {StatusCode}", slug, response.StatusCode);
                return null;
            }

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

            var fullUrl = $"{BASE_PATH}/modelslug/{Uri.EscapeDataString(modelSlug)}";
            _logger.LogInformation("Calling PremiumVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get premium vehicle by model slug {ModelSlug}. Status: {StatusCode}", modelSlug, response.StatusCode);
                return null;
            }

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

            var fullUrl = $"{BASE_PATH}/active?limit={limit}";
            _logger.LogInformation("Calling PremiumVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get active premium vehicles. Status: {StatusCode}", response.StatusCode);
                return Enumerable.Empty<PremiumVehicleSummaryDto>();
            }

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

            var fullUrl = $"{BASE_PATH}/toppriority?limit={limit}";
            _logger.LogInformation("Calling PremiumVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get top priority premium vehicles. Status: {StatusCode}", response.StatusCode);
                return Enumerable.Empty<PremiumVehicleSummaryDto>();
            }

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

            var fullUrl = $"{BASE_PATH}/brand/{Uri.EscapeDataString(brandName)}?limit={limit}";
            _logger.LogInformation("Calling PremiumVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get premium vehicles by brand {BrandName}. Status: {StatusCode}", brandName, response.StatusCode);
                return Enumerable.Empty<PremiumVehicleSummaryDto>();
            }

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

            var fullUrl = $"{BASE_PATH}/city/{Uri.EscapeDataString(city)}?limit={limit}";
            _logger.LogInformation("Calling PremiumVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get premium vehicles by city {City}. Status: {StatusCode}", city, response.StatusCode);
                return Enumerable.Empty<PremiumVehicleSummaryDto>();
            }

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

            var fullUrl = $"{BASE_PATH}/vehicletype/{Uri.EscapeDataString(vehicleType)}?limit={limit}";
            _logger.LogInformation("Calling PremiumVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get premium vehicles by vehicle type {VehicleType}. Status: {StatusCode}", vehicleType, response.StatusCode);
                return Enumerable.Empty<PremiumVehicleSummaryDto>();
            }

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

            var fullUrl = $"{BASE_PATH}/pricerange?minPrice={minPrice}&maxPrice={maxPrice}&limit={limit}";
            _logger.LogInformation("Calling PremiumVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get premium vehicles by price range {MinPrice}-{MaxPrice}. Status: {StatusCode}", minPrice, maxPrice, response.StatusCode);
                return Enumerable.Empty<PremiumVehicleSummaryDto>();
            }

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

            var fullUrl = $"{BASE_PATH}/search?term={Uri.EscapeDataString(searchTerm)}&limit={limit}";
            _logger.LogInformation("Calling PremiumVehicle API: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to search premium vehicles with term {SearchTerm}. Status: {StatusCode}", searchTerm, response.StatusCode);
                return Enumerable.Empty<PremiumVehicleSummaryDto>();
            }

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

            _logger.LogInformation("Creating new premium vehicle with model slug: {ModelSlug}", request.ModelSlug);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync(BASE_PATH, request)
            );

            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new InvalidOperationException("Duplicate entry - Slug or ModelSlug already exists");

            response.EnsureSuccessStatusCode();

            var result = await ReadApiResponseAsync<PremiumVehicleResponseDto>(response)
                         ?? throw new Exception("Invalid response from API");

            InvalidateAllCaches();

            return result;
        }

        public async Task<PremiumVehicleResponseDto?> UpdateAsync(string id, PremiumVehicleRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException(nameof(id));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            _logger.LogInformation("Updating premium vehicle {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PutAsJsonAsync($"{BASE_PATH}/{id}", request)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to update premium vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
                return null;
            }

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

            _logger.LogInformation("Deleting premium vehicle {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.DeleteAsync($"{BASE_PATH}/{id}")
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            _logger.LogWarning("Failed to delete premium vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        public async Task<bool> ActivateAsync(string id, DateTime? endDate = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            _logger.LogInformation("Activating premium vehicle {Id} with end date {EndDate}", id, endDate);

            var request = new { endDate };
            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync($"{BASE_PATH}/{id}/activate", request)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            _logger.LogWarning("Failed to activate premium vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        public async Task<bool> DeactivateAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            _logger.LogInformation("Deactivating premium vehicle {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsync($"{BASE_PATH}/{id}/deactivate", null)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            _logger.LogWarning("Failed to deactivate premium vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        public async Task<bool> UpdatePriorityAsync(string id, int priority)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            _logger.LogInformation("Updating priority for premium vehicle {Id} to {Priority}", id, priority);

            var request = new { priority };
            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PatchAsJsonAsync($"{BASE_PATH}/{id}/priority", request)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                InvalidateAllCaches();
                return true;
            }

            _logger.LogWarning("Failed to update priority for premium vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        public async Task<bool> BulkUpdatePriorityAsync(Dictionary<string, int> priorityUpdates)
        {
            if (priorityUpdates == null || !priorityUpdates.Any())
                return false;

            _logger.LogInformation("Bulk updating priorities for {Count} vehicles", priorityUpdates.Count);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PatchAsJsonAsync($"{BASE_PATH}/bulk-priority", priorityUpdates)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateAllCaches();
                return true;
            }

            _logger.LogWarning("Failed to bulk update priorities. Status: {StatusCode}", response.StatusCode);
            return false;
        }

        public async Task<bool> AddRatingAsync(string id, PremiumVehicleUserRatingDto rating)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            if (rating == null)
                return false;

            _logger.LogInformation("Adding rating for premium vehicle {Id}", id);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync($"{BASE_PATH}/{id}/ratings", rating)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateVehicleCache(id);
                return true;
            }

            _logger.LogWarning("Failed to add rating for premium vehicle {Id}. Status: {StatusCode}", id, response.StatusCode);
            return false;
        }

        public async Task<bool> IncrementViewsAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsync($"{BASE_PATH}/{id}/increment-views", null)
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
                _httpClient.PostAsync($"{BASE_PATH}/{id}/increment-likes", null)
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
                _httpClient.PostAsync($"{BASE_PATH}/{id}/increment-shares", null)
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
                _httpClient.PostAsync($"{BASE_PATH}/{id}/increment-enquiries", null)
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

            var queryParams = new List<string> { $"slug={Uri.EscapeDataString(slug)}" };
            if (!string.IsNullOrEmpty(excludeId))
                queryParams.Add($"excludeId={excludeId}");

            var fullUrl = $"{BASE_PATH}/is-slug-unique?{string.Join("&", queryParams)}";
            _logger.LogDebug("Checking slug uniqueness: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to check slug uniqueness. Status: {StatusCode}", response.StatusCode);
                return false;
            }

            var result = await ReadApiResponseAsync<bool>(response);
            return result;
        }

        public async Task<bool> IsModelSlugUniqueAsync(string modelSlug, string? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(modelSlug))
                return false;

            var queryParams = new List<string> { $"modelSlug={Uri.EscapeDataString(modelSlug)}" };
            if (!string.IsNullOrEmpty(excludeId))
                queryParams.Add($"excludeId={excludeId}");

            var fullUrl = $"{BASE_PATH}/is-model-slug-unique?{string.Join("&", queryParams)}";
            _logger.LogDebug("Checking model slug uniqueness: {Url}", fullUrl);

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(fullUrl)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to check model slug uniqueness. Status: {StatusCode}", response.StatusCode);
                return false;
            }

            var result = await ReadApiResponseAsync<bool>(response);
            return result;
        }
    }
}