using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System.Net;
using System.Net.Http.Json;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
{
    public class ColorService : IColorService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ColorService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreakerPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public ColorService(
            HttpClient httpClient,
            IMemoryCache memoryCache,
            ILogger<ColorService> logger)
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
                            "Retry {RetryCount} after {Delay}s due to: {StatusCode}",
                            retryCount,
                            timespan.TotalSeconds,
                            outcome.Result?.StatusCode);
                    });

            // Circuit breaker policy - CORRECTED VERSION
            _circuitBreakerPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => IsTransientError(r.StatusCode))
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (outcome, breakDelay) =>
                    {
                        var exception = outcome.Exception;
                        var statusCode = outcome.Result?.StatusCode;
                        _logger.LogError(exception,
                            "Circuit broken for {BreakDelay}s due to repeated failures. Last status code: {StatusCode}",
                            breakDelay.TotalSeconds,
                            statusCode);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit reset - service is available again");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Circuit half-open - testing service availability");
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

        public async Task<IEnumerable<ColorResponseDto>> GetAllColorsAsync()
        {
            const string cacheKey = "all_colors";

            // Try to get from cache first
            if (_cache.TryGetValue(cacheKey, out IEnumerable<ColorResponseDto> cachedColors))
            {
                _logger.LogDebug("Returning cached colors");
                return cachedColors ?? Enumerable.Empty<ColorResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                // Double-check cache after acquiring lock
                if (_cache.TryGetValue(cacheKey, out cachedColors))
                {
                    return cachedColors ?? Enumerable.Empty<ColorResponseDto>();
                }

                _logger.LogInformation("Fetching all colors from API");

                // Combine policies for resilient execution
                var combinedPolicy = _retryPolicy.WrapAsync(_circuitBreakerPolicy);
                var response = await combinedPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all colors", requestId);
                    return await _httpClient.GetAsync("api/v1/colors");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get colors. Status: {StatusCode}", response.StatusCode);

                    // Log error details for 500 errors
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }

                    return Enumerable.Empty<ColorResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var colors = JsonConvert.DeserializeObject<IEnumerable<ColorResponseDto>>(content);

                // Cache for 5 minutes
                if (colors != null && colors.Any())
                {
                    _cache.Set(cacheKey, colors, TimeSpan.FromMinutes(5));
                }

                return colors ?? Enumerable.Empty<ColorResponseDto>();
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError(ex, "Circuit breaker is open - service unavailable");
                throw new InvalidOperationException("Color service is currently unavailable. Please try again later.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching colors");
                throw;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<IEnumerable<ColorResponseDto>> GetActiveColorsAsync()
        {
            const string cacheKey = "active_colors";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<ColorResponseDto> cachedColors))
            {
                return cachedColors ?? Enumerable.Empty<ColorResponseDto>();
            }

            try
            {
                var combinedPolicy = _retryPolicy.WrapAsync(_circuitBreakerPolicy);
                var response = await combinedPolicy.ExecuteAsync(() => _httpClient.GetAsync("api/v1/colors/active"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<ColorResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var colors = JsonConvert.DeserializeObject<IEnumerable<ColorResponseDto>>(content);

                if (colors != null && colors.Any())
                {
                    _cache.Set(cacheKey, colors, TimeSpan.FromMinutes(5));
                }

                return colors ?? Enumerable.Empty<ColorResponseDto>();
            }
            catch (BrokenCircuitException)
            {
                return Enumerable.Empty<ColorResponseDto>();
            }
        }

        public async Task<IEnumerable<ColorResponseDto>> GetColorsByDisplayOrderAsync()
        {
            const string cacheKey = "ordered_colors";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<ColorResponseDto> cachedColors))
            {
                return cachedColors ?? Enumerable.Empty<ColorResponseDto>();
            }

            try
            {
                var combinedPolicy = _retryPolicy.WrapAsync(_circuitBreakerPolicy);
                var response = await combinedPolicy.ExecuteAsync(() => _httpClient.GetAsync("api/v1/colors/ordered"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<ColorResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var colors = JsonConvert.DeserializeObject<IEnumerable<ColorResponseDto>>(content);

                if (colors != null && colors.Any())
                {
                    _cache.Set(cacheKey, colors, TimeSpan.FromMinutes(5));
                }

                return colors ?? Enumerable.Empty<ColorResponseDto>();
            }
            catch (BrokenCircuitException)
            {
                return Enumerable.Empty<ColorResponseDto>();
            }
        }

        public async Task<ColorResponseDto?> GetColorByIdAsync(Guid colorId)
        {
            string cacheKey = $"color_{colorId}";

            if (_cache.TryGetValue(cacheKey, out ColorResponseDto cachedColor))
            {
                return cachedColor;
            }

            try
            {
                var combinedPolicy = _retryPolicy.WrapAsync(_circuitBreakerPolicy);
                var response = await combinedPolicy.ExecuteAsync(() => _httpClient.GetAsync($"api/v1/colors/{colorId}"));

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var color = JsonConvert.DeserializeObject<ColorResponseDto>(content);

                if (color != null)
                {
                    _cache.Set(cacheKey, color, TimeSpan.FromMinutes(10));
                }

                return color;
            }
            catch (BrokenCircuitException)
            {
                return null;
            }
        }

        public async Task<ColorResponseDto?> GetColorByCodeAsync(string code)
        {
            string cacheKey = $"color_code_{code}";

            if (_cache.TryGetValue(cacheKey, out ColorResponseDto cachedColor))
            {
                return cachedColor;
            }

            try
            {
                var combinedPolicy = _retryPolicy.WrapAsync(_circuitBreakerPolicy);
                var response = await combinedPolicy.ExecuteAsync(() => _httpClient.GetAsync($"api/v1/colors/code/{code}"));

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var color = JsonConvert.DeserializeObject<ColorResponseDto>(content);

                if (color != null)
                {
                    _cache.Set(cacheKey, color, TimeSpan.FromMinutes(10));
                }

                return color;
            }
            catch (BrokenCircuitException)
            {
                return null;
            }
        }

        public async Task<ColorResponseDto> CreateColorAsync(ColorCreateDto createDto)
        {
            try
            {
                var combinedPolicy = _retryPolicy.WrapAsync(_circuitBreakerPolicy);
                var response = await combinedPolicy.ExecuteAsync(() => _httpClient.PostAsJsonAsync("api/v1/colors", createDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Color already exists");

                response.EnsureSuccessStatusCode();

                // Invalidate cache after create
                InvalidateColorCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ColorResponseDto>(content)!;
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError(ex, "Service unavailable while creating color");
                throw new InvalidOperationException("Color service is currently unavailable. Please try again later.", ex);
            }
        }

        public async Task<ColorResponseDto?> UpdateColorAsync(Guid colorId, ColorUpdateDto updateDto)
        {
            try
            {
                var combinedPolicy = _retryPolicy.WrapAsync(_circuitBreakerPolicy);
                var response = await combinedPolicy.ExecuteAsync(() => _httpClient.PutAsJsonAsync($"api/v1/colors/{colorId}", updateDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate color code");

                if (!response.IsSuccessStatusCode)
                    return null;

                // Invalidate cache after update
                InvalidateColorCaches();
                _cache.Remove($"color_{colorId}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ColorResponseDto>(content);
            }
            catch (BrokenCircuitException)
            {
                return null;
            }
        }

        public async Task<bool> ToggleColorStatusAsync(Guid colorId, bool isActive)
        {
            try
            {
                var combinedPolicy = _retryPolicy.WrapAsync(_circuitBreakerPolicy);
                var response = await combinedPolicy.ExecuteAsync(() => _httpClient.PatchAsync(
                    $"api/v1/colors/{colorId}/toggle/{isActive}",
                    null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateColorCaches();
                    _cache.Remove($"color_{colorId}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (BrokenCircuitException)
            {
                return false;
            }
        }

        public async Task<bool> ReorderColorsAsync(Dictionary<Guid, int> orderMap)
        {
            try
            {
                var combinedPolicy = _retryPolicy.WrapAsync(_circuitBreakerPolicy);
                var response = await combinedPolicy.ExecuteAsync(() => _httpClient.PostAsJsonAsync("api/v1/colors/reorder", orderMap));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateColorCaches();
                }

                return response.IsSuccessStatusCode;
            }
            catch (BrokenCircuitException)
            {
                return false;
            }
        }

        public async Task<bool> ValidateHexCodeAsync(string hexCode)
        {
            try
            {
                var combinedPolicy = _retryPolicy.WrapAsync(_circuitBreakerPolicy);
                var response = await combinedPolicy.ExecuteAsync(() => _httpClient.GetAsync($"api/v1/colors/validate-hex?hexCode={hexCode}"));

                if (!response.IsSuccessStatusCode)
                    return false;

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<dynamic>(content);

                return result?.isValid ?? false;
            }
            catch (BrokenCircuitException)
            {
                return false;
            }
        }

        public async Task<string?> ConvertHexToRgbAsync(string hexCode)
        {
            try
            {
                var combinedPolicy = _retryPolicy.WrapAsync(_circuitBreakerPolicy);
                var response = await combinedPolicy.ExecuteAsync(() => _httpClient.GetAsync($"api/v1/colors/convert-hex-to-rgb?hexCode={hexCode}"));

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<dynamic>(content);

                return result?.rgbValue;
            }
            catch (BrokenCircuitException)
            {
                return null;
            }
        }

        public async Task<bool> DeleteColorAsync(Guid colorId)
        {
            try
            {
                var combinedPolicy = _retryPolicy.WrapAsync(_circuitBreakerPolicy);
                var response = await combinedPolicy.ExecuteAsync(() => _httpClient.DeleteAsync($"api/v1/colors/{colorId}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateColorCaches();
                    _cache.Remove($"color_{colorId}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (BrokenCircuitException)
            {
                return false;
            }
        }

        private void InvalidateColorCaches()
        {
            _cache.Remove("all_colors");
            _cache.Remove("active_colors");
            _cache.Remove("ordered_colors");
        }
    }
}