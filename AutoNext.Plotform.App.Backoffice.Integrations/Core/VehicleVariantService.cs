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
    public class VehicleVariantService : IVehicleVariantService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<VehicleVariantService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public VehicleVariantService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<VehicleVariantService> logger)
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
                            "Retry {RetryCount} after {Delay}s for VehicleVariant API due to: {StatusCode}",
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

        public async Task<IEnumerable<VehicleVariantResponseDto>> GetAllVariantsAsync()
        {
            const string cacheKey = "all_vehicle_variants";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<VehicleVariantResponseDto> cachedVariants))
            {
                _logger.LogDebug("Returning cached vehicle variants");
                return cachedVariants ?? Enumerable.Empty<VehicleVariantResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedVariants))
                {
                    return cachedVariants ?? Enumerable.Empty<VehicleVariantResponseDto>();
                }

                _logger.LogInformation("Fetching all vehicle variants from API");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all variants", requestId);
                    return await _httpClient.GetAsync("api/v1/vehiclevariants");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get variants. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<VehicleVariantResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var variants = JsonConvert.DeserializeObject<IEnumerable<VehicleVariantResponseDto>>(content);

                if (variants != null && variants.Any())
                {
                    _cache.Set(cacheKey, variants, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    });
                }

                return variants ?? Enumerable.Empty<VehicleVariantResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching variants");
                return Enumerable.Empty<VehicleVariantResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<IEnumerable<VehicleVariantResponseDto>> GetActiveVariantsAsync()
        {
            const string cacheKey = "active_vehicle_variants";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<VehicleVariantResponseDto> cachedVariants))
            {
                return cachedVariants ?? Enumerable.Empty<VehicleVariantResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/vehiclevariants/active"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<VehicleVariantResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var variants = JsonConvert.DeserializeObject<IEnumerable<VehicleVariantResponseDto>>(content);

                if (variants != null && variants.Any())
                {
                    _cache.Set(cacheKey, variants, TimeSpan.FromMinutes(5));
                }

                return variants ?? Enumerable.Empty<VehicleVariantResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active variants");
                return Enumerable.Empty<VehicleVariantResponseDto>();
            }
        }

        public async Task<IEnumerable<VehicleVariantResponseDto>> GetAvailableVariantsAsync()
        {
            const string cacheKey = "available_vehicle_variants";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<VehicleVariantResponseDto> cachedVariants))
            {
                return cachedVariants ?? Enumerable.Empty<VehicleVariantResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/vehiclevariants/available"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<VehicleVariantResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var variants = JsonConvert.DeserializeObject<IEnumerable<VehicleVariantResponseDto>>(content);

                if (variants != null && variants.Any())
                {
                    _cache.Set(cacheKey, variants, TimeSpan.FromMinutes(5));
                }

                return variants ?? Enumerable.Empty<VehicleVariantResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching available variants");
                return Enumerable.Empty<VehicleVariantResponseDto>();
            }
        }

        public async Task<IEnumerable<VehicleVariantResponseDto>> GetVariantsByModelAsync(Guid modelId)
        {
            string cacheKey = $"variants_model_{modelId}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<VehicleVariantResponseDto> cachedVariants))
            {
                return cachedVariants ?? Enumerable.Empty<VehicleVariantResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/vehiclevariants/model/{modelId}"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<VehicleVariantResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var variants = JsonConvert.DeserializeObject<IEnumerable<VehicleVariantResponseDto>>(content);

                if (variants != null && variants.Any())
                {
                    _cache.Set(cacheKey, variants, TimeSpan.FromMinutes(10));
                }

                return variants ?? Enumerable.Empty<VehicleVariantResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching variants by model: {ModelId}", modelId);
                return Enumerable.Empty<VehicleVariantResponseDto>();
            }
        }

        public async Task<VehicleVariantResponseDto?> GetVariantByIdAsync(Guid variantId)
        {
            string cacheKey = $"variant_{variantId}";

            if (_cache.TryGetValue(cacheKey, out VehicleVariantResponseDto cachedVariant))
            {
                return cachedVariant;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/vehiclevariants/{variantId}"));

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var variant = JsonConvert.DeserializeObject<VehicleVariantResponseDto>(content);

                if (variant != null)
                {
                    _cache.Set(cacheKey, variant, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return variant;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching variant by ID: {VariantId}", variantId);
                return null;
            }
        }

        public async Task<VehicleVariantResponseDto?> GetVariantByCodeAsync(string code)
        {
            string cacheKey = $"variant_code_{code}";

            if (_cache.TryGetValue(cacheKey, out VehicleVariantResponseDto cachedVariant))
            {
                return cachedVariant;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/vehiclevariants/code/{code}"));

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var variant = JsonConvert.DeserializeObject<VehicleVariantResponseDto>(content);

                if (variant != null)
                {
                    _cache.Set(cacheKey, variant, TimeSpan.FromMinutes(15));
                }

                return variant;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching variant by code: {Code}", code);
                return null;
            }
        }

        public async Task<VehicleVariantSpecsDto> GetVariantSpecsAsync(Guid variantId)
        {
            string cacheKey = $"variant_specs_{variantId}";

            if (_cache.TryGetValue(cacheKey, out VehicleVariantSpecsDto cachedSpecs))
            {
                return cachedSpecs;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/vehiclevariants/{variantId}/specs"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get variant specs for {VariantId}, returning empty specs", variantId);
                    return new VehicleVariantSpecsDto();
                }

                var content = await response.Content.ReadAsStringAsync();
                var specs = JsonConvert.DeserializeObject<VehicleVariantSpecsDto>(content) ?? new VehicleVariantSpecsDto();

                _cache.Set(cacheKey, specs, TimeSpan.FromMinutes(10));

                return specs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching variant specs: {VariantId}", variantId);
                return new VehicleVariantSpecsDto();
            }
        }

        public async Task<decimal> GetVariantPriceRangeAsync(Guid modelId)
        {
            string cacheKey = $"variant_price_range_{modelId}";

            if (_cache.TryGetValue(cacheKey, out decimal cachedPriceRange))
            {
                return cachedPriceRange;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/vehiclevariants/model/{modelId}/price-range"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get price range for model {ModelId}, returning 0", modelId);
                    return 0;
                }

                var content = await response.Content.ReadAsStringAsync();
                var priceRangeResponse = JsonConvert.DeserializeObject<dynamic>(content);
                var priceRange = (decimal?)priceRangeResponse?.priceRange ?? 0;

                _cache.Set(cacheKey, priceRange, TimeSpan.FromMinutes(10));

                return priceRange;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching price range for model: {ModelId}", modelId);
                return 0;
            }
        }

        public async Task<IEnumerable<VehicleVariantResponseDto>> FilterVariantsAsync(VehicleVariantFilterDto filterDto)
        {
            string cacheKey = $"variants_filter_{JsonConvert.SerializeObject(filterDto).GetHashCode()}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<VehicleVariantResponseDto> cachedVariants))
            {
                return cachedVariants ?? Enumerable.Empty<VehicleVariantResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/vehiclevariants/filter", filterDto));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<VehicleVariantResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var variants = JsonConvert.DeserializeObject<IEnumerable<VehicleVariantResponseDto>>(content);

                if (variants != null && variants.Any())
                {
                    _cache.Set(cacheKey, variants, TimeSpan.FromMinutes(2));
                }

                return variants ?? Enumerable.Empty<VehicleVariantResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering variants");
                return Enumerable.Empty<VehicleVariantResponseDto>();
            }
        }

        public async Task<VehicleVariantResponseDto> CreateVariantAsync(VehicleVariantCreateDto createDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/vehiclevariants", createDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Variant with this code or name already exists for the model");

                response.EnsureSuccessStatusCode();

                // Invalidate caches after create
                InvalidateVariantCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<VehicleVariantResponseDto>(content)!;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating variant");
                throw new InvalidOperationException("Failed to create variant. Please try again.", ex);
            }
        }

        public async Task<VehicleVariantResponseDto?> UpdateVariantAsync(Guid variantId, VehicleVariantUpdateDto updateDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/vehiclevariants/{variantId}", updateDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate variant code or name");

                if (!response.IsSuccessStatusCode)
                    return null;

                // Invalidate caches after update
                InvalidateVariantCaches();
                _cache.Remove($"variant_{variantId}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<VehicleVariantResponseDto>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating variant: {VariantId}", variantId);
                return null;
            }
        }

        public async Task<bool> ToggleVariantStatusAsync(Guid variantId, bool isActive)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/vehiclevariants/{variantId}/toggle/{isActive}", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateVariantCaches();
                    _cache.Remove($"variant_{variantId}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling variant status: {VariantId}", variantId);
                return false;
            }
        }

        public async Task<bool> ToggleAvailabilityAsync(Guid variantId, bool isAvailable)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/vehiclevariants/{variantId}/availability/{isAvailable}", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateVariantCaches();
                    _cache.Remove($"variant_{variantId}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling variant availability: {VariantId}", variantId);
                return false;
            }
        }

        public async Task<bool> ReorderVariantsAsync(Dictionary<Guid, int> orderMap)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/vehiclevariants/reorder", orderMap));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateVariantCaches();
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering variants");
                return false;
            }
        }

        public async Task<bool> DeleteVariantAsync(Guid variantId)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/vehiclevariants/{variantId}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateVariantCaches();
                    _cache.Remove($"variant_{variantId}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting variant: {VariantId}", variantId);
                return false;
            }
        }

        private void InvalidateVariantCaches()
        {
            _cache.Remove("all_vehicle_variants");
            _cache.Remove("active_vehicle_variants");
            _cache.Remove("available_vehicle_variants");
            // Model-specific caches would need more sophisticated invalidation
            // Consider using cache tags or patterns for model-specific invalidation
        }
    }
}