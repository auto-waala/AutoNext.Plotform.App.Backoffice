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
    public class VehicleModelService : IVehicleModelService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<VehicleModelService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public VehicleModelService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<VehicleModelService> logger)
        {
            _httpClient = httpClient;
            _cache = memoryCache;
            _logger = logger;

            _httpClient.Timeout = TimeSpan.FromSeconds(30);

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
                            "Retry {RetryCount} after {Delay}s for VehicleModel API due to: {StatusCode}",
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

        public async Task<IEnumerable<VehicleModelResponseDto>> GetAllAsync()
        {
            const string cacheKey = "all_vehicle_models";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<VehicleModelResponseDto> cachedModels))
            {
                _logger.LogDebug("Returning cached vehicle models");
                return cachedModels ?? Enumerable.Empty<VehicleModelResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                // Double-check after acquiring lock
                if (_cache.TryGetValue(cacheKey, out cachedModels))
                    return cachedModels ?? Enumerable.Empty<VehicleModelResponseDto>();

                _logger.LogInformation("Fetching all vehicle models from API");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all models", requestId);
                    return await _httpClient.GetAsync("api/v1/vehicle-models");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get models. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<VehicleModelResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var models = JsonConvert.DeserializeObject<IEnumerable<VehicleModelResponseDto>>(content);

                if (models != null && models.Any())
                {
                    _cache.Set(cacheKey, models, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    });
                }

                return models ?? Enumerable.Empty<VehicleModelResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching vehicle models");
                return Enumerable.Empty<VehicleModelResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<VehicleModelResponseDto?> GetByIdAsync(Guid id)
        {
            string cacheKey = $"vehicle_model_{id}";

            if (_cache.TryGetValue(cacheKey, out VehicleModelResponseDto cachedModel))
                return cachedModel;

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/vehicle-models/{id}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get model by ID {Id}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var model = JsonConvert.DeserializeObject<VehicleModelResponseDto>(content);

                if (model != null)
                {
                    _cache.Set(cacheKey, model, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return model;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching vehicle model by ID: {Id}", id);
                return null;
            }
        }

        public async Task<IEnumerable<VehicleModelResponseDto>> GetByBrandAsync(Guid brandId)
        {
            string cacheKey = $"vehicle_models_brand_{brandId}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<VehicleModelResponseDto> cachedModels))
                return cachedModels ?? Enumerable.Empty<VehicleModelResponseDto>();

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/vehicle-models/brand/{brandId}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get models for brand {BrandId}. Status: {StatusCode}", brandId, response.StatusCode);
                    return Enumerable.Empty<VehicleModelResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var models = JsonConvert.DeserializeObject<IEnumerable<VehicleModelResponseDto>>(content);

                if (models != null && models.Any())
                {
                    _cache.Set(cacheKey, models, TimeSpan.FromMinutes(10));
                }

                return models ?? Enumerable.Empty<VehicleModelResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching vehicle models by brand: {BrandId}", brandId);
                return Enumerable.Empty<VehicleModelResponseDto>();
            }
        }

        public async Task<VehicleModelResponseDto> CreateAsync(VehicleModelCreateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/vehicle-models", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("A vehicle model with this code, slug, or name already exists for the brand.");

                response.EnsureSuccessStatusCode();

                InvalidateModelCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<VehicleModelResponseDto>(content)!;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating vehicle model");
                throw new InvalidOperationException("Failed to create vehicle model. Please try again.", ex);
            }
        }

        public async Task<VehicleModelResponseDto?> UpdateAsync(Guid id, VehicleModelUpdateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/vehicle-models/{id}", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate model code or slug.");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to update vehicle model {Id}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                InvalidateModelCaches();
                _cache.Remove($"vehicle_model_{id}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<VehicleModelResponseDto>(content);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating vehicle model: {Id}", id);
                return null;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/vehicle-models/{id}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateModelCaches();
                    _cache.Remove($"vehicle_model_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting vehicle model: {Id}", id);
                return false;
            }
        }

        private void InvalidateModelCaches()
        {
            _cache.Remove("all_vehicle_models");
            // Brand-specific caches require more sophisticated invalidation
            // Consider cache tags or a pattern-based eviction strategy per brandId
        }
    }
}