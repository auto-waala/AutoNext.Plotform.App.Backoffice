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
    public class WarrantyTypeService : IWarrantyTypeService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<WarrantyTypeService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public WarrantyTypeService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<WarrantyTypeService> logger)
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
                            "Retry {RetryCount} after {Delay}s for WarrantyType API due to: {StatusCode}",
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

        public async Task<IEnumerable<WarrantyTypeResponseDto>> GetAllWarrantyTypesAsync()
        {
            const string cacheKey = "all_warranty_types";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<WarrantyTypeResponseDto> cachedWarranties))
            {
                _logger.LogDebug("Returning cached warranty types");
                return cachedWarranties ?? Enumerable.Empty<WarrantyTypeResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedWarranties))
                {
                    return cachedWarranties ?? Enumerable.Empty<WarrantyTypeResponseDto>();
                }

                _logger.LogInformation("Fetching all warranty types from API");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all warranty types", requestId);
                    return await _httpClient.GetAsync("api/v1/warrantytypes");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get warranty types. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<WarrantyTypeResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var warrantyTypes = JsonConvert.DeserializeObject<IEnumerable<WarrantyTypeResponseDto>>(content);

                if (warrantyTypes != null && warrantyTypes.Any())
                {
                    _cache.Set(cacheKey, warrantyTypes, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    });
                }

                return warrantyTypes ?? Enumerable.Empty<WarrantyTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching warranty types");
                return Enumerable.Empty<WarrantyTypeResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<IEnumerable<WarrantyTypeResponseDto>> GetActiveWarrantyTypesAsync()
        {
            const string cacheKey = "active_warranty_types";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<WarrantyTypeResponseDto> cachedWarranties))
            {
                return cachedWarranties ?? Enumerable.Empty<WarrantyTypeResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/warrantytypes/active"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<WarrantyTypeResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var warrantyTypes = JsonConvert.DeserializeObject<IEnumerable<WarrantyTypeResponseDto>>(content);

                if (warrantyTypes != null && warrantyTypes.Any())
                {
                    _cache.Set(cacheKey, warrantyTypes, TimeSpan.FromMinutes(5));
                }

                return warrantyTypes ?? Enumerable.Empty<WarrantyTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active warranty types");
                return Enumerable.Empty<WarrantyTypeResponseDto>();
            }
        }

        public async Task<IEnumerable<WarrantyTypeResponseDto>> GetTransferableWarrantyTypesAsync()
        {
            const string cacheKey = "transferable_warranty_types";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<WarrantyTypeResponseDto> cachedWarranties))
            {
                return cachedWarranties ?? Enumerable.Empty<WarrantyTypeResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/warrantytypes/transferable"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<WarrantyTypeResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var warrantyTypes = JsonConvert.DeserializeObject<IEnumerable<WarrantyTypeResponseDto>>(content);

                if (warrantyTypes != null && warrantyTypes.Any())
                {
                    _cache.Set(cacheKey, warrantyTypes, TimeSpan.FromMinutes(5));
                }

                return warrantyTypes ?? Enumerable.Empty<WarrantyTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transferable warranty types");
                return Enumerable.Empty<WarrantyTypeResponseDto>();
            }
        }

        public async Task<WarrantyTypeResponseDto?> GetWarrantyTypeByIdAsync(Guid warrantyTypeId)
        {
            string cacheKey = $"warranty_type_{warrantyTypeId}";

            if (_cache.TryGetValue(cacheKey, out WarrantyTypeResponseDto cachedWarranty))
            {
                return cachedWarranty;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/warrantytypes/{warrantyTypeId}"));

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var warrantyType = JsonConvert.DeserializeObject<WarrantyTypeResponseDto>(content);

                if (warrantyType != null)
                {
                    _cache.Set(cacheKey, warrantyType, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return warrantyType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching warranty type by ID: {WarrantyTypeId}", warrantyTypeId);
                return null;
            }
        }

        public async Task<WarrantyTypeResponseDto?> GetWarrantyTypeByCodeAsync(string code)
        {
            string cacheKey = $"warranty_type_code_{code}";

            if (_cache.TryGetValue(cacheKey, out WarrantyTypeResponseDto cachedWarranty))
            {
                return cachedWarranty;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/warrantytypes/code/{code}"));

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var warrantyType = JsonConvert.DeserializeObject<WarrantyTypeResponseDto>(content);

                if (warrantyType != null)
                {
                    _cache.Set(cacheKey, warrantyType, TimeSpan.FromMinutes(10));
                }

                return warrantyType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching warranty type by code: {Code}", code);
                return null;
            }
        }

        public async Task<IEnumerable<WarrantyTypeResponseDto>> GetWarrantyTypesByCategoryAsync(string categoryCode)
        {
            string cacheKey = $"warranty_types_category_{categoryCode}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<WarrantyTypeResponseDto> cachedWarranties))
            {
                return cachedWarranties ?? Enumerable.Empty<WarrantyTypeResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/warrantytypes/category/{categoryCode}"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<WarrantyTypeResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var warrantyTypes = JsonConvert.DeserializeObject<IEnumerable<WarrantyTypeResponseDto>>(content);

                if (warrantyTypes != null && warrantyTypes.Any())
                {
                    _cache.Set(cacheKey, warrantyTypes, TimeSpan.FromMinutes(5));
                }

                return warrantyTypes ?? Enumerable.Empty<WarrantyTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching warranty types by category: {CategoryCode}", categoryCode);
                return Enumerable.Empty<WarrantyTypeResponseDto>();
            }
        }

        public async Task<WarrantyTypeResponseDto?> GetBestWarrantyByCategoryAsync(string categoryCode)
        {
            string cacheKey = $"best_warranty_category_{categoryCode}";

            if (_cache.TryGetValue(cacheKey, out WarrantyTypeResponseDto cachedWarranty))
            {
                return cachedWarranty;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/warrantytypes/category/{categoryCode}/best"));

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var warrantyType = JsonConvert.DeserializeObject<WarrantyTypeResponseDto>(content);

                if (warrantyType != null)
                {
                    _cache.Set(cacheKey, warrantyType, TimeSpan.FromMinutes(5));
                }

                return warrantyType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching best warranty by category: {CategoryCode}", categoryCode);
                return null;
            }
        }

        public async Task<IEnumerable<WarrantyTypeResponseDto>> FilterWarrantyTypesAsync(WarrantyTypeFilterDto filterDto)
        {
            string cacheKey = $"warranty_types_filter_{JsonConvert.SerializeObject(filterDto).GetHashCode()}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<WarrantyTypeResponseDto> cachedWarranties))
            {
                return cachedWarranties ?? Enumerable.Empty<WarrantyTypeResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/warrantytypes/filter", filterDto));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<WarrantyTypeResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var warrantyTypes = JsonConvert.DeserializeObject<IEnumerable<WarrantyTypeResponseDto>>(content);

                if (warrantyTypes != null && warrantyTypes.Any())
                {
                    _cache.Set(cacheKey, warrantyTypes, TimeSpan.FromMinutes(2));
                }

                return warrantyTypes ?? Enumerable.Empty<WarrantyTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering warranty types");
                return Enumerable.Empty<WarrantyTypeResponseDto>();
            }
        }

        public async Task<WarrantyComparisonDto> CompareWarrantiesAsync(Guid warrantyIdA, Guid warrantyIdB)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/warrantytypes/compare/{warrantyIdA}/{warrantyIdB}"));

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new ArgumentException($"Warranty comparison failed: {response.StatusCode} - {errorContent}");
                }

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<WarrantyComparisonDto>(content)!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing warranties: {WarrantyIdA} vs {WarrantyIdB}", warrantyIdA, warrantyIdB);
                throw new ArgumentException("Failed to compare warranties", ex);
            }
        }

        public async Task<WarrantyTypeResponseDto> CreateWarrantyTypeAsync(WarrantyTypeCreateDto createDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/warrantytypes", createDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Warranty type with this code or name already exists");

                response.EnsureSuccessStatusCode();

                // Invalidate caches after create
                InvalidateWarrantyTypeCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<WarrantyTypeResponseDto>(content)!;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating warranty type");
                throw new InvalidOperationException("Failed to create warranty type. Please try again.", ex);
            }
        }

        public async Task<WarrantyTypeResponseDto?> UpdateWarrantyTypeAsync(Guid warrantyTypeId, WarrantyTypeUpdateDto updateDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/warrantytypes/{warrantyTypeId}", updateDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate warranty type code or name");

                if (!response.IsSuccessStatusCode)
                    return null;

                // Invalidate caches after update
                InvalidateWarrantyTypeCaches();
                _cache.Remove($"warranty_type_{warrantyTypeId}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<WarrantyTypeResponseDto>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating warranty type: {WarrantyTypeId}", warrantyTypeId);
                return null;
            }
        }

        public async Task<bool> ToggleWarrantyTypeStatusAsync(Guid warrantyTypeId, bool isActive)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/warrantytypes/{warrantyTypeId}/toggle/{isActive}", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateWarrantyTypeCaches();
                    _cache.Remove($"warranty_type_{warrantyTypeId}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling warranty type status: {WarrantyTypeId}", warrantyTypeId);
                return false;
            }
        }

        public async Task<bool> ReorderWarrantyTypesAsync(Dictionary<Guid, int> orderMap)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/warrantytypes/reorder", orderMap));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateWarrantyTypeCaches();
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering warranty types");
                return false;
            }
        }

        public async Task<bool> DeleteWarrantyTypeAsync(Guid warrantyTypeId)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/warrantytypes/{warrantyTypeId}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateWarrantyTypeCaches();
                    _cache.Remove($"warranty_type_{warrantyTypeId}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting warranty type: {WarrantyTypeId}", warrantyTypeId);
                return false;
            }
        }

        private void InvalidateWarrantyTypeCaches()
        {
            _cache.Remove("all_warranty_types");
            _cache.Remove("active_warranty_types");
            _cache.Remove("transferable_warranty_types");
            // Note: Category-specific caches would need more sophisticated invalidation
        }
    }
}