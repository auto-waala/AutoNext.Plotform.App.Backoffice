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
    public class BrandService : IBrandService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<BrandService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public BrandService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<BrandService> logger)
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
                            "Retry {RetryCount} after {Delay}s for Brand API due to: {StatusCode}",
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

        public async Task<IEnumerable<BrandResponseDto>> GetAllBrandsAsync()
        {
            const string cacheKey = "all_brands";

            // Try to get from cache first
            if (_cache.TryGetValue(cacheKey, out IEnumerable<BrandResponseDto> cachedBrands))
            {
                _logger.LogDebug("Returning cached brands");
                return cachedBrands ?? Enumerable.Empty<BrandResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                // Double-check cache after acquiring lock
                if (_cache.TryGetValue(cacheKey, out cachedBrands))
                {
                    return cachedBrands ?? Enumerable.Empty<BrandResponseDto>();
                }

                _logger.LogInformation("Fetching all brands from API");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all brands", requestId);
                    return await _httpClient.GetAsync("api/v1/brands");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get brands. Status: {StatusCode}", response.StatusCode);

                    // Log error details for 500 errors
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }

                    return Enumerable.Empty<BrandResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var brands = JsonConvert.DeserializeObject<IEnumerable<BrandResponseDto>>(content);

                // Cache for 5 minutes
                if (brands != null && brands.Any())
                {
                    _cache.Set(cacheKey, brands, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    });
                }

                return brands ?? Enumerable.Empty<BrandResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching brands");
                return Enumerable.Empty<BrandResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<IEnumerable<BrandResponseDto>> GetActiveBrandsAsync()
        {
            const string cacheKey = "active_brands";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<BrandResponseDto> cachedBrands))
            {
                return cachedBrands ?? Enumerable.Empty<BrandResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/brands/active"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<BrandResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var brands = JsonConvert.DeserializeObject<IEnumerable<BrandResponseDto>>(content);

                if (brands != null && brands.Any())
                {
                    _cache.Set(cacheKey, brands, TimeSpan.FromMinutes(5));
                }

                return brands ?? Enumerable.Empty<BrandResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active brands");
                return Enumerable.Empty<BrandResponseDto>();
            }
        }

        public async Task<BrandResponseDto?> GetBrandByIdAsync(Guid brandId)
        {
            string cacheKey = $"brand_{brandId}";

            if (_cache.TryGetValue(cacheKey, out BrandResponseDto cachedBrand))
            {
                return cachedBrand;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/brands/{brandId}"));

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var brand = JsonConvert.DeserializeObject<BrandResponseDto>(content);

                if (brand != null)
                {
                    _cache.Set(cacheKey, brand, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return brand;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching brand by ID: {BrandId}", brandId);
                return null;
            }
        }

        public async Task<IEnumerable<BrandResponseDto>> GetBrandsByCategoryAsync(string categoryCode)
        {
            string cacheKey = $"brands_category_{categoryCode}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<BrandResponseDto> cachedBrands))
            {
                return cachedBrands ?? Enumerable.Empty<BrandResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/brands/category/{categoryCode}"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<BrandResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var brands = JsonConvert.DeserializeObject<IEnumerable<BrandResponseDto>>(content);

                if (brands != null && brands.Any())
                {
                    _cache.Set(cacheKey, brands, TimeSpan.FromMinutes(5));
                }

                return brands ?? Enumerable.Empty<BrandResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching brands by category: {CategoryCode}", categoryCode);
                return Enumerable.Empty<BrandResponseDto>();
            }
        }

        public async Task<IEnumerable<BrandResponseDto>> GetBrandsByCountryAsync(string countryCode)
        {
            string cacheKey = $"brands_country_{countryCode}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<BrandResponseDto> cachedBrands))
            {
                return cachedBrands ?? Enumerable.Empty<BrandResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/brands/country/{countryCode}"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<BrandResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var brands = JsonConvert.DeserializeObject<IEnumerable<BrandResponseDto>>(content);

                if (brands != null && brands.Any())
                {
                    _cache.Set(cacheKey, brands, TimeSpan.FromMinutes(5));
                }

                return brands ?? Enumerable.Empty<BrandResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching brands by country: {CountryCode}", countryCode);
                return Enumerable.Empty<BrandResponseDto>();
            }
        }

        public async Task<BrandResponseDto> CreateBrandAsync(BrandCreateDto createDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/brands", createDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Brand already exists");

                response.EnsureSuccessStatusCode();

                // Invalidate cache after create
                InvalidateBrandCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<BrandResponseDto>(content)!;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating brand");
                throw new InvalidOperationException("Failed to create brand. Please try again.", ex);
            }
        }

        public async Task<BrandResponseDto?> UpdateBrandAsync(Guid brandId, BrandUpdateDto updateDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/brands/{brandId}", updateDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate brand name or code");

                if (!response.IsSuccessStatusCode)
                    return null;

                // Invalidate cache after update
                InvalidateBrandCaches();
                _cache.Remove($"brand_{brandId}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<BrandResponseDto>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating brand: {BrandId}", brandId);
                return null;
            }
        }

        public async Task<bool> ToggleBrandStatusAsync(Guid brandId, bool isActive)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/brands/{brandId}/toggle/{isActive}", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateBrandCaches();
                    _cache.Remove($"brand_{brandId}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling brand status: {BrandId}", brandId);
                return false;
            }
        }

        public async Task<bool> DeleteBrandAsync(Guid brandId)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/brands/{brandId}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateBrandCaches();
                    _cache.Remove($"brand_{brandId}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting brand: {BrandId}", brandId);
                return false;
            }
        }

        private void InvalidateBrandCaches()
        {
            _cache.Remove("all_brands");
            _cache.Remove("active_brands");
            // Note: For category and country caches, you might need more sophisticated invalidation
            // In a production environment, consider using a cache key pattern or distributed cache
        }
    }
}