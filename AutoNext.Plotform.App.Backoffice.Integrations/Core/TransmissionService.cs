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
    public class TransmissionService : ITransmissionService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TransmissionService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public TransmissionService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<TransmissionService> logger)
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
                            "Retry {RetryCount} after {Delay}s for Transmission API due to: {StatusCode}",
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

        public async Task<IEnumerable<TransmissionResponseDto>> GetAllAsync(bool onlyActive = false)
        {
            string cacheKey = onlyActive ? "active_transmissions" : "all_transmissions";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<TransmissionResponseDto> cachedTransmissions))
            {
                _logger.LogDebug("Returning cached transmissions (onlyActive: {OnlyActive})", onlyActive);
                return cachedTransmissions ?? Enumerable.Empty<TransmissionResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedTransmissions))
                {
                    return cachedTransmissions ?? Enumerable.Empty<TransmissionResponseDto>();
                }

                _logger.LogInformation("Fetching transmissions from API (onlyActive: {OnlyActive})", onlyActive);

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get transmissions (onlyActive: {OnlyActive})", requestId, onlyActive);
                    return await _httpClient.GetAsync($"api/v1/transmissions{(onlyActive ? "/active" : "")}");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get transmissions. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<TransmissionResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var transmissions = JsonConvert.DeserializeObject<IEnumerable<TransmissionResponseDto>>(content);

                if (transmissions != null && transmissions.Any())
                {
                    _cache.Set(cacheKey, transmissions, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return transmissions ?? Enumerable.Empty<TransmissionResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching transmissions (onlyActive: {OnlyActive})", onlyActive);
                return Enumerable.Empty<TransmissionResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<IEnumerable<TransmissionResponseDto>> GetByGearsCountAsync(int gearsCount)
        {
            string cacheKey = $"transmissions_gears_{gearsCount}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<TransmissionResponseDto> cachedTransmissions))
            {
                _logger.LogDebug("Returning cached transmissions for {GearsCount} gears", gearsCount);
                return cachedTransmissions ?? Enumerable.Empty<TransmissionResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/transmissions/gears/{gearsCount}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get transmissions by gears count {GearsCount}. Status: {StatusCode}", gearsCount, response.StatusCode);
                    return Enumerable.Empty<TransmissionResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var transmissions = JsonConvert.DeserializeObject<IEnumerable<TransmissionResponseDto>>(content);

                if (transmissions != null && transmissions.Any())
                {
                    _cache.Set(cacheKey, transmissions, TimeSpan.FromMinutes(15));
                }

                return transmissions ?? Enumerable.Empty<TransmissionResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transmissions by gears count: {GearsCount}", gearsCount);
                return Enumerable.Empty<TransmissionResponseDto>();
            }
        }

        public async Task<TransmissionResponseDto?> GetByIdAsync(Guid id)
        {
            string cacheKey = $"transmission_{id}";

            if (_cache.TryGetValue(cacheKey, out TransmissionResponseDto cachedTransmission))
            {
                return cachedTransmission;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/transmissions/{id}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get transmission {TransmissionId}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var transmission = JsonConvert.DeserializeObject<TransmissionResponseDto>(content);

                if (transmission != null)
                {
                    _cache.Set(cacheKey, transmission, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return transmission;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transmission by ID: {TransmissionId}", id);
                return null;
            }
        }

        public async Task<TransmissionResponseDto> CreateAsync(TransmissionCreateDto createDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/transmissions", createDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Transmission with this name or code already exists");

                response.EnsureSuccessStatusCode();

                // Invalidate caches after create
                InvalidateTransmissionCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<TransmissionResponseDto>(content)!;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating transmission");
                throw new InvalidOperationException("Failed to create transmission. Please try again.", ex);
            }
        }

        public async Task<TransmissionResponseDto?> UpdateAsync(TransmissionUpdateDto updateDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync("api/v1/transmissions", updateDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate transmission name or code");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Update failed for transmission. Status: {StatusCode}", response.StatusCode);
                    return null;
                }

                // Invalidate caches after update
                InvalidateTransmissionCaches();

                var content = await response.Content.ReadAsStringAsync();
                var updatedTransmission = JsonConvert.DeserializeObject<TransmissionResponseDto>(content);

                // Remove specific item cache if ID is available
                if (updatedTransmission?.Id != null)
                {
                    _cache.Remove($"transmission_{updatedTransmission.Id}");
                }

                return updatedTransmission;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating transmission");
                return null;
            }
        }

        public async Task<bool> ToggleActiveAsync(Guid id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/transmissions/{id}/toggle-active", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateTransmissionCaches();
                    _cache.Remove($"transmission_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling transmission active status: {TransmissionId}", id);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/transmissions/{id}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateTransmissionCaches();
                    _cache.Remove($"transmission_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting transmission: {TransmissionId}", id);
                return false;
            }
        }

        private void InvalidateTransmissionCaches()
        {
            _cache.Remove("all_transmissions");
            _cache.Remove("active_transmissions");
            _logger.LogDebug("Transmission caches invalidated");
        }
    }
}