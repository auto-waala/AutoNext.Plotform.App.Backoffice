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
    public class FeatureService : IFeatureService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<FeatureService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public FeatureService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<FeatureService> logger)
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
                            "Retry {RetryCount} after {Delay}s for Feature API due to: {StatusCode}",
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

        public async Task<IEnumerable<FeatureResponseDto>> GetAllAsync()
        {
            const string cacheKey = "all_features";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeatureResponseDto> cachedFeatures))
            {
                _logger.LogDebug("Returning cached features");
                return cachedFeatures ?? Enumerable.Empty<FeatureResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedFeatures))
                {
                    return cachedFeatures ?? Enumerable.Empty<FeatureResponseDto>();
                }

                _logger.LogInformation("Fetching all features from API");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all features", requestId);
                    return await _httpClient.GetAsync("api/v1/features");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get features. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<FeatureResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var features = JsonConvert.DeserializeObject<IEnumerable<FeatureResponseDto>>(content);

                if (features != null && features.Any())
                {
                    _cache.Set(cacheKey, features, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return features ?? Enumerable.Empty<FeatureResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching features");
                return Enumerable.Empty<FeatureResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<IEnumerable<FeatureResponseDto>> GetActiveAsync()
        {
            const string cacheKey = "active_features";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeatureResponseDto> cachedFeatures))
            {
                _logger.LogDebug("Returning cached active features");
                return cachedFeatures ?? Enumerable.Empty<FeatureResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/features/active"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get active features. Status: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<FeatureResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var features = JsonConvert.DeserializeObject<IEnumerable<FeatureResponseDto>>(content);

                if (features != null && features.Any())
                {
                    _cache.Set(cacheKey, features, TimeSpan.FromMinutes(10));
                }

                return features ?? Enumerable.Empty<FeatureResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active features");
                return Enumerable.Empty<FeatureResponseDto>();
            }
        }

        public async Task<FeatureResponseDto?> GetByIdAsync(Guid id)
        {
            string cacheKey = $"feature_{id}";

            if (_cache.TryGetValue(cacheKey, out FeatureResponseDto cachedFeature))
            {
                return cachedFeature;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/features/{id}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get feature {FeatureId}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var feature = JsonConvert.DeserializeObject<FeatureResponseDto>(content);

                if (feature != null)
                {
                    _cache.Set(cacheKey, feature, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return feature;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching feature by ID: {FeatureId}", id);
                return null;
            }
        }

        public async Task<IEnumerable<FeatureResponseDto>> GetByCategoryAsync(string category)
        {
            string cacheKey = $"features_category_{category}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<FeatureResponseDto> cachedFeatures))
            {
                _logger.LogDebug("Returning cached features for category: {Category}", category);
                return cachedFeatures ?? Enumerable.Empty<FeatureResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/features/category/{category}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get features by category {Category}. Status: {StatusCode}", category, response.StatusCode);
                    return Enumerable.Empty<FeatureResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var features = JsonConvert.DeserializeObject<IEnumerable<FeatureResponseDto>>(content);

                if (features != null && features.Any())
                {
                    _cache.Set(cacheKey, features, TimeSpan.FromMinutes(15));
                }

                return features ?? Enumerable.Empty<FeatureResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching features by category: {Category}", category);
                return Enumerable.Empty<FeatureResponseDto>();
            }
        }

        public async Task<FeatureResponseDto> CreateAsync(FeatureCreateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/features", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Feature with this code or name already exists");

                response.EnsureSuccessStatusCode();

                // Invalidate caches after create
                InvalidateFeatureCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<FeatureResponseDto>(content)!;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating feature");
                throw new InvalidOperationException("Failed to create feature. Please try again.", ex);
            }
        }

        public async Task<FeatureResponseDto?> UpdateAsync(Guid id, FeatureUpdateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/features/{id}", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate feature code or name");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Update failed for feature {FeatureId}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                // Invalidate caches after update
                InvalidateFeatureCaches();
                _cache.Remove($"feature_{id}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<FeatureResponseDto>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating feature: {FeatureId}", id);
                return null;
            }
        }

        public async Task<bool> ToggleStatusAsync(Guid id, bool isActive)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/features/{id}/toggle/{isActive}", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateFeatureCaches();
                    _cache.Remove($"feature_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling feature status: {FeatureId}", id);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/features/{id}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateFeatureCaches();
                    _cache.Remove($"feature_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting feature: {FeatureId}", id);
                return false;
            }
        }

        private void InvalidateFeatureCaches()
        {
            _cache.Remove("all_features");
            _cache.Remove("active_features");
            // Category-specific caches preserved to avoid over-invalidation
            _logger.LogDebug("Feature caches invalidated");
        }
    }
}