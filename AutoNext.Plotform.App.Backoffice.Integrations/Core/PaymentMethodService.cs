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
    public class PaymentMethodService : IPaymentMethodService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PaymentMethodService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public PaymentMethodService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<PaymentMethodService> logger)
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
                            "Retry {RetryCount} after {Delay}s for PaymentMethod API due to: {StatusCode}",
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

        public async Task<IEnumerable<PaymentMethodResponseDto>> GetAllAsync()
        {
            const string cacheKey = "all_payment_methods";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<PaymentMethodResponseDto> cachedMethods))
            {
                _logger.LogDebug("Returning cached payment methods");
                return cachedMethods ?? Enumerable.Empty<PaymentMethodResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedMethods))
                {
                    return cachedMethods ?? Enumerable.Empty<PaymentMethodResponseDto>();
                }

                _logger.LogInformation("Fetching all payment methods from API");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all payment methods", requestId);
                    return await _httpClient.GetAsync("api/v1/payment-methods");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get payment methods. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<PaymentMethodResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var methods = JsonConvert.DeserializeObject<IEnumerable<PaymentMethodResponseDto>>(content);

                if (methods != null && methods.Any())
                {
                    _cache.Set(cacheKey, methods, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return methods ?? Enumerable.Empty<PaymentMethodResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching payment methods");
                return Enumerable.Empty<PaymentMethodResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<IEnumerable<PaymentMethodResponseDto>> GetActiveAsync()
        {
            const string cacheKey = "active_payment_methods";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<PaymentMethodResponseDto> cachedMethods))
            {
                _logger.LogDebug("Returning cached active payment methods");
                return cachedMethods ?? Enumerable.Empty<PaymentMethodResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/payment-methods/active"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get active payment methods. Status: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<PaymentMethodResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var methods = JsonConvert.DeserializeObject<IEnumerable<PaymentMethodResponseDto>>(content);

                if (methods != null && methods.Any())
                {
                    _cache.Set(cacheKey, methods, TimeSpan.FromMinutes(10));
                }

                return methods ?? Enumerable.Empty<PaymentMethodResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active payment methods");
                return Enumerable.Empty<PaymentMethodResponseDto>();
            }
        }

        public async Task<PaymentMethodResponseDto?> GetByIdAsync(Guid id)
        {
            string cacheKey = $"payment_method_{id}";

            if (_cache.TryGetValue(cacheKey, out PaymentMethodResponseDto cachedMethod))
            {
                return cachedMethod;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/payment-methods/{id}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get payment method {PaymentMethodId}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var method = JsonConvert.DeserializeObject<PaymentMethodResponseDto>(content);

                if (method != null)
                {
                    _cache.Set(cacheKey, method, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return method;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching payment method by ID: {PaymentMethodId}", id);
                return null;
            }
        }

        public async Task<IEnumerable<PaymentMethodResponseDto>> GetByTypeAsync(string type)
        {
            string cacheKey = $"payment_methods_type_{type}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<PaymentMethodResponseDto> cachedMethods))
            {
                _logger.LogDebug("Returning cached payment methods for type: {Type}", type);
                return cachedMethods ?? Enumerable.Empty<PaymentMethodResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/payment-methods/type/{type}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get payment methods by type {Type}. Status: {StatusCode}", type, response.StatusCode);
                    return Enumerable.Empty<PaymentMethodResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var methods = JsonConvert.DeserializeObject<IEnumerable<PaymentMethodResponseDto>>(content);

                if (methods != null && methods.Any())
                {
                    _cache.Set(cacheKey, methods, TimeSpan.FromMinutes(15));
                }

                return methods ?? Enumerable.Empty<PaymentMethodResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching payment methods by type: {Type}", type);
                return Enumerable.Empty<PaymentMethodResponseDto>();
            }
        }

        public async Task<PaymentMethodResponseDto> CreateAsync(PaymentMethodCreateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/payment-methods", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Payment method with this code or name already exists");

                response.EnsureSuccessStatusCode();

                // Invalidate caches after create
                InvalidatePaymentMethodCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<PaymentMethodResponseDto>(content)!;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating payment method");
                throw new InvalidOperationException("Failed to create payment method. Please try again.", ex);
            }
        }

        public async Task<PaymentMethodResponseDto?> UpdateAsync(Guid id, PaymentMethodUpdateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/payment-methods/{id}", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate payment method code or name");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Update failed for payment method {PaymentMethodId}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                // Invalidate caches after update
                InvalidatePaymentMethodCaches();
                _cache.Remove($"payment_method_{id}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<PaymentMethodResponseDto>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment method: {PaymentMethodId}", id);
                return null;
            }
        }

        public async Task<bool> ToggleStatusAsync(Guid id, bool isActive)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/payment-methods/{id}/toggle/{isActive}", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidatePaymentMethodCaches();
                    _cache.Remove($"payment_method_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling payment method status: {PaymentMethodId}", id);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/payment-methods/{id}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidatePaymentMethodCaches();
                    _cache.Remove($"payment_method_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting payment method: {PaymentMethodId}", id);
                return false;
            }
        }

        private void InvalidatePaymentMethodCaches()
        {
            _cache.Remove("all_payment_methods");
            _cache.Remove("active_payment_methods");
            // Type-specific caches are not invalidated here as it would be too broad
            // Consider using cache tags or more granular invalidation for production
            _logger.LogDebug("Payment method caches invalidated");
        }
    }
}