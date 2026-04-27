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
    public class TaxRateService : ITaxRateService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TaxRateService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public TaxRateService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<TaxRateService> logger)
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
                            "Retry {RetryCount} after {Delay}s for TaxRate API due to: {StatusCode}",
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

        public async Task<IEnumerable<TaxRateResponseDto>> GetAllAsync()
        {
            const string cacheKey = "all_tax_rates";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<TaxRateResponseDto> cachedRates))
            {
                _logger.LogDebug("Returning cached tax rates");
                return cachedRates ?? Enumerable.Empty<TaxRateResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedRates))
                    return cachedRates ?? Enumerable.Empty<TaxRateResponseDto>();

                _logger.LogInformation("Fetching all tax rates from API");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all tax rates", requestId);
                    return await _httpClient.GetAsync("api/v1/tax-rates");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get tax rates. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<TaxRateResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var rates = JsonConvert.DeserializeObject<IEnumerable<TaxRateResponseDto>>(content);

                if (rates != null && rates.Any())
                {
                    // Tax rates change infrequently but have date-based effectivity,
                    // so keep cache short to reflect EffectiveFrom/EffectiveTo changes
                    _cache.Set(cacheKey, rates, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    });
                }

                return rates ?? Enumerable.Empty<TaxRateResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching tax rates");
                return Enumerable.Empty<TaxRateResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<IEnumerable<TaxRateResponseDto>> GetActiveAsync()
        {
            const string cacheKey = "active_tax_rates";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<TaxRateResponseDto> cachedRates))
                return cachedRates ?? Enumerable.Empty<TaxRateResponseDto>();

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/tax-rates/active"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get active tax rates. Status: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<TaxRateResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var rates = JsonConvert.DeserializeObject<IEnumerable<TaxRateResponseDto>>(content);

                if (rates != null && rates.Any())
                {
                    // Shorter TTL for active rates since EffectiveFrom/EffectiveTo
                    // can cause a rate to become inactive without any write operation
                    _cache.Set(cacheKey, rates, TimeSpan.FromMinutes(2));
                }

                return rates ?? Enumerable.Empty<TaxRateResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active tax rates");
                return Enumerable.Empty<TaxRateResponseDto>();
            }
        }

        public async Task<TaxRateResponseDto?> GetByIdAsync(Guid id)
        {
            string cacheKey = $"tax_rate_{id}";

            if (_cache.TryGetValue(cacheKey, out TaxRateResponseDto cachedRate))
                return cachedRate;

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/tax-rates/{id}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get tax rate by ID {Id}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var rate = JsonConvert.DeserializeObject<TaxRateResponseDto>(content);

                if (rate != null)
                {
                    _cache.Set(cacheKey, rate, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return rate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tax rate by ID: {Id}", id);
                return null;
            }
        }

        public async Task<TaxRateResponseDto> CreateAsync(TaxRateCreateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/tax-rates", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("A tax rate with this code or name already exists.");

                response.EnsureSuccessStatusCode();

                InvalidateTaxRateCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<TaxRateResponseDto>(content)!;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating tax rate");
                throw new InvalidOperationException("Failed to create tax rate. Please try again.", ex);
            }
        }

        public async Task<TaxRateResponseDto?> UpdateAsync(Guid id, TaxRateUpdateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/tax-rates/{id}", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate tax rate code or name.");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to update tax rate {Id}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                InvalidateTaxRateCaches();
                _cache.Remove($"tax_rate_{id}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<TaxRateResponseDto>(content);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tax rate: {Id}", id);
                return null;
            }
        }

        public async Task<bool> ToggleStatusAsync(Guid id, bool isActive)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/tax-rates/{id}/toggle/{isActive}", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateTaxRateCaches();
                    _cache.Remove($"tax_rate_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling tax rate status: {Id}", id);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/tax-rates/{id}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateTaxRateCaches();
                    _cache.Remove($"tax_rate_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tax rate: {Id}", id);
                return false;
            }
        }

        private void InvalidateTaxRateCaches()
        {
            _cache.Remove("all_tax_rates");
            _cache.Remove("active_tax_rates");
        }
    }
}