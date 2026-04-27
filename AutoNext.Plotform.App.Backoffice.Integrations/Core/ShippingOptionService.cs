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
    public class ShippingOptionService : IShippingOptionService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ShippingOptionService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public ShippingOptionService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<ShippingOptionService> logger)
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
                            "Retry {RetryCount} after {Delay}s for ShippingOption API due to: {StatusCode}",
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

        public async Task<IEnumerable<ShippingOptionResponseDto>> GetAllAsync()
        {
            const string cacheKey = "all_shipping_options";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<ShippingOptionResponseDto> cachedOptions))
            {
                _logger.LogDebug("Returning cached shipping options");
                return cachedOptions ?? Enumerable.Empty<ShippingOptionResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedOptions))
                    return cachedOptions ?? Enumerable.Empty<ShippingOptionResponseDto>();

                _logger.LogInformation("Fetching all shipping options from API");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all shipping options", requestId);
                    return await _httpClient.GetAsync("api/v1/shipping-options");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get shipping options. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<ShippingOptionResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var options = JsonConvert.DeserializeObject<IEnumerable<ShippingOptionResponseDto>>(content);

                if (options != null && options.Any())
                {
                    _cache.Set(cacheKey, options, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    });
                }

                return options ?? Enumerable.Empty<ShippingOptionResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching shipping options");
                return Enumerable.Empty<ShippingOptionResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<IEnumerable<ShippingOptionResponseDto>> GetActiveAsync()
        {
            const string cacheKey = "active_shipping_options";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<ShippingOptionResponseDto> cachedOptions))
                return cachedOptions ?? Enumerable.Empty<ShippingOptionResponseDto>();

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/shipping-options/active"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get active shipping options. Status: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<ShippingOptionResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var options = JsonConvert.DeserializeObject<IEnumerable<ShippingOptionResponseDto>>(content);

                if (options != null && options.Any())
                {
                    _cache.Set(cacheKey, options, TimeSpan.FromMinutes(5));
                }

                return options ?? Enumerable.Empty<ShippingOptionResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active shipping options");
                return Enumerable.Empty<ShippingOptionResponseDto>();
            }
        }

        public async Task<ShippingOptionResponseDto?> GetByIdAsync(Guid id)
        {
            string cacheKey = $"shipping_option_{id}";

            if (_cache.TryGetValue(cacheKey, out ShippingOptionResponseDto cachedOption))
                return cachedOption;

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/shipping-options/{id}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get shipping option by ID {Id}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var option = JsonConvert.DeserializeObject<ShippingOptionResponseDto>(content);

                if (option != null)
                {
                    _cache.Set(cacheKey, option, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return option;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching shipping option by ID: {Id}", id);
                return null;
            }
        }

        public async Task<ShippingOptionResponseDto> CreateAsync(ShippingOptionCreateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/shipping-options", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("A shipping option with this code or name already exists.");

                response.EnsureSuccessStatusCode();

                InvalidateShippingOptionCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ShippingOptionResponseDto>(content)!;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating shipping option");
                throw new InvalidOperationException("Failed to create shipping option. Please try again.", ex);
            }
        }

        public async Task<ShippingOptionResponseDto?> UpdateAsync(Guid id, ShippingOptionUpdateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/shipping-options/{id}", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate shipping option code or name.");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to update shipping option {Id}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                InvalidateShippingOptionCaches();
                _cache.Remove($"shipping_option_{id}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ShippingOptionResponseDto>(content);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating shipping option: {Id}", id);
                return null;
            }
        }

        public async Task<bool> ToggleStatusAsync(Guid id, bool isActive)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/shipping-options/{id}/toggle/{isActive}", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateShippingOptionCaches();
                    _cache.Remove($"shipping_option_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling shipping option status: {Id}", id);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/shipping-options/{id}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateShippingOptionCaches();
                    _cache.Remove($"shipping_option_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting shipping option: {Id}", id);
                return false;
            }
        }

        private void InvalidateShippingOptionCaches()
        {
            _cache.Remove("all_shipping_options");
            _cache.Remove("active_shipping_options");
        }
    }
}