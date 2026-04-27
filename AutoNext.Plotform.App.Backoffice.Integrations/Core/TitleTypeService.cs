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
    public class TitleTypeService : ITitleTypeService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TitleTypeService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public TitleTypeService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<TitleTypeService> logger)
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
                            "Retry {RetryCount} after {Delay}s for TitleType API due to: {StatusCode}",
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

        public async Task<IEnumerable<TitleTypeResponseDto>> GetAllAsync()
        {
            const string cacheKey = "all_title_types";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<TitleTypeResponseDto> cachedTypes))
            {
                _logger.LogDebug("Returning cached title types");
                return cachedTypes ?? Enumerable.Empty<TitleTypeResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedTypes))
                    return cachedTypes ?? Enumerable.Empty<TitleTypeResponseDto>();

                _logger.LogInformation("Fetching all title types from API");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all title types", requestId);
                    return await _httpClient.GetAsync("api/v1/title-types");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get title types. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<TitleTypeResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var types = JsonConvert.DeserializeObject<IEnumerable<TitleTypeResponseDto>>(content);

                if (types != null && types.Any())
                {
                    _cache.Set(cacheKey, types, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    });
                }

                return types ?? Enumerable.Empty<TitleTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching title types");
                return Enumerable.Empty<TitleTypeResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<IEnumerable<TitleTypeResponseDto>> GetActiveAsync()
        {
            const string cacheKey = "active_title_types";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<TitleTypeResponseDto> cachedTypes))
                return cachedTypes ?? Enumerable.Empty<TitleTypeResponseDto>();

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/title-types/active"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get active title types. Status: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<TitleTypeResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var types = JsonConvert.DeserializeObject<IEnumerable<TitleTypeResponseDto>>(content);

                if (types != null && types.Any())
                {
                    _cache.Set(cacheKey, types, TimeSpan.FromMinutes(5));
                }

                return types ?? Enumerable.Empty<TitleTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active title types");
                return Enumerable.Empty<TitleTypeResponseDto>();
            }
        }

        public async Task<TitleTypeResponseDto?> GetByIdAsync(Guid id)
        {
            string cacheKey = $"title_type_{id}";

            if (_cache.TryGetValue(cacheKey, out TitleTypeResponseDto cachedType))
                return cachedType;

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/title-types/{id}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get title type by ID {Id}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var type = JsonConvert.DeserializeObject<TitleTypeResponseDto>(content);

                if (type != null)
                {
                    _cache.Set(cacheKey, type, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return type;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching title type by ID: {Id}", id);
                return null;
            }
        }

        public async Task<TitleTypeResponseDto> CreateAsync(TitleTypeCreateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/title-types", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("A title type with this code or name already exists.");

                response.EnsureSuccessStatusCode();

                InvalidateTitleTypeCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<TitleTypeResponseDto>(content)!;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating title type");
                throw new InvalidOperationException("Failed to create title type. Please try again.", ex);
            }
        }

        public async Task<TitleTypeResponseDto?> UpdateAsync(Guid id, TitleTypeUpdateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/title-types/{id}", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate title type code or name.");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to update title type {Id}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                InvalidateTitleTypeCaches();
                _cache.Remove($"title_type_{id}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<TitleTypeResponseDto>(content);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating title type: {Id}", id);
                return null;
            }
        }

        public async Task<bool> ToggleStatusAsync(Guid id, bool isActive)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/title-types/{id}/toggle/{isActive}", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateTitleTypeCaches();
                    _cache.Remove($"title_type_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling title type status: {Id}", id);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/title-types/{id}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateTitleTypeCaches();
                    _cache.Remove($"title_type_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting title type: {Id}", id);
                return false;
            }
        }

        private void InvalidateTitleTypeCaches()
        {
            _cache.Remove("all_title_types");
            _cache.Remove("active_title_types");
        }
    }
}