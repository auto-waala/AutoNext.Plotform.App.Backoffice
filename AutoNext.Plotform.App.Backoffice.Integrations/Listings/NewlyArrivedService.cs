using AutoNext.Plotform.App.Backoffice.Integrations.Listings;
using AutoNext.Plotform.App.Backoffice.Models.Common;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using System.Net;
using System.Net.Http.Json;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Listings
{
    public class NewlyArrivedService : INewlyArrivedService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<NewlyArrivedService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public NewlyArrivedService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<NewlyArrivedService> logger)
        {
            _httpClient = httpClient;
            _cache = memoryCache;
            _logger = logger;

            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _retryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => IsTransientError(r.StatusCode))
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(outcome.Exception,
                            "Retry {RetryCount} after {Delay}s due to {StatusCode}",
                            retryCount, timespan.TotalSeconds, outcome.Result?.StatusCode);
                    });
        }

        private bool IsTransientError(HttpStatusCode statusCode) =>
            statusCode == HttpStatusCode.InternalServerError ||
            statusCode == HttpStatusCode.ServiceUnavailable ||
            statusCode == HttpStatusCode.BadGateway ||
            statusCode == HttpStatusCode.GatewayTimeout ||
            statusCode == HttpStatusCode.RequestTimeout;

        // ✅ GENERIC API RESPONSE HANDLER
        private async Task<T?> ReadApiResponseAsync<T>(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();

            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<T>>(content);

            if (apiResponse == null || !apiResponse.IsSuccess)
            {
                _logger.LogWarning("API response invalid or failed");
                return default;
            }

            return apiResponse.Data;
        }

        public async Task<PagedResult<NewlyArrivedResponseDto>> GetAllAsync(int page, int pageSize)
        {
            string cacheKey = $"newly_arrived_all_{page}_{pageSize}";

            if (_cache.TryGetValue(cacheKey, out PagedResult<NewlyArrivedResponseDto> cached))
                return cached;

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cached))
                    return cached;

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/newlyarrived?page={page}&pageSize={pageSize}")
                );

                if (!response.IsSuccessStatusCode)
                    return new PagedResult<NewlyArrivedResponseDto>();

                var result = await ReadApiResponseAsync<PagedResult<NewlyArrivedResponseDto>>(response)
                             ?? new PagedResult<NewlyArrivedResponseDto>();

                _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                    SlidingExpiration = TimeSpan.FromMinutes(10)
                });

                return result;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<NewlyArrivedResponseDto?> GetByIdAsync(string id)
        {
            string cacheKey = $"newly_arrived_{id}";

            if (_cache.TryGetValue(cacheKey, out NewlyArrivedResponseDto cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/newlyarrived/{id}")
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var item = await ReadApiResponseAsync<NewlyArrivedResponseDto>(response);

            if (item != null)
                _cache.Set(cacheKey, item, TimeSpan.FromMinutes(60));

            return item;
        }

        public async Task<NewlyArrivedResponseDto?> GetByModelSlugAsync(string modelSlug)
        {
            string cacheKey = $"newly_arrived_slug_{modelSlug}";

            if (_cache.TryGetValue(cacheKey, out NewlyArrivedResponseDto cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/newlyarrived/slug/{modelSlug}")
            );

            if (!response.IsSuccessStatusCode)
                return null;

            var item = await ReadApiResponseAsync<NewlyArrivedResponseDto>(response);

            if (item != null)
                _cache.Set(cacheKey, item, TimeSpan.FromMinutes(60));

            return item;
        }

        public async Task<IEnumerable<NewlyArrivedResponseDto>> GetFeaturedArrivalsAsync(int limit = 10)
        {
            string cacheKey = $"newly_arrived_featured_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<NewlyArrivedResponseDto> cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/newlyarrived/featured?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<NewlyArrivedResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<NewlyArrivedResponseDto>>(response)
                        ?? Enumerable.Empty<NewlyArrivedResponseDto>();

            _cache.Set(cacheKey, items, TimeSpan.FromMinutes(30));

            return items;
        }

        public async Task<IEnumerable<NewlyArrivedResponseDto>> GetWeeklyArrivalsAsync(int limit = 20)
        {
            string cacheKey = $"newly_arrived_weekly_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<NewlyArrivedResponseDto> cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/newlyarrived/weekly?limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<NewlyArrivedResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<NewlyArrivedResponseDto>>(response)
                        ?? Enumerable.Empty<NewlyArrivedResponseDto>();

            _cache.Set(cacheKey, items, TimeSpan.FromMinutes(60));

            return items;
        }

        public async Task<IEnumerable<NewlyArrivedResponseDto>> GetMonthlyArrivalsAsync(int month, int year, int limit = 20)
        {
            string cacheKey = $"newly_arrived_monthly_{month}_{year}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<NewlyArrivedResponseDto> cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/newlyarrived/monthly?month={month}&year={year}&limit={limit}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<NewlyArrivedResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<NewlyArrivedResponseDto>>(response)
                        ?? Enumerable.Empty<NewlyArrivedResponseDto>();

            _cache.Set(cacheKey, items, TimeSpan.FromMinutes(60));

            return items;
        }

        public async Task<IEnumerable<NewlyArrivedResponseDto>> GetYearlyArrivalsAsync(int year)
        {
            string cacheKey = $"newly_arrived_yearly_{year}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<NewlyArrivedResponseDto> cached))
                return cached;

            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync($"api/v1/newlyarrived/yearly?year={year}")
            );

            if (!response.IsSuccessStatusCode)
                return Enumerable.Empty<NewlyArrivedResponseDto>();

            var items = await ReadApiResponseAsync<IEnumerable<NewlyArrivedResponseDto>>(response)
                        ?? Enumerable.Empty<NewlyArrivedResponseDto>();

            _cache.Set(cacheKey, items, TimeSpan.FromMinutes(60));

            return items;
        }

        public async Task<NewlyArrivedResponseDto> CreateAsync(NewlyArrivedRequestDto request, string publishedBy)
        {
            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PostAsJsonAsync("api/v1/newlyarrived", request)
            );

            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new InvalidOperationException("Duplicate entry");

            response.EnsureSuccessStatusCode();

            InvalidateCaches();

            return await ReadApiResponseAsync<NewlyArrivedResponseDto>(response)
                   ?? throw new Exception("Invalid response");
        }

        public async Task<NewlyArrivedResponseDto?> UpdateAsync(string id, NewlyArrivedRequestDto request)
        {
            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PutAsJsonAsync($"api/v1/newlyarrived/{id}", request)
            );

            if (!response.IsSuccessStatusCode)
                return null;

            InvalidateCaches();
            _cache.Remove($"newly_arrived_{id}");

            return await ReadApiResponseAsync<NewlyArrivedResponseDto>(response);
        }

        public async Task<bool> PublishAsync(string id, string publishedBy)
        {
            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PatchAsync($"api/v1/newlyarrived/{id}/publish", JsonContent.Create(new { publishedBy }))
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateCaches();
                _cache.Remove($"newly_arrived_{id}");
            }

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UnpublishAsync(string id)
        {
            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.PatchAsync($"api/v1/newlyarrived/{id}/unpublish", null)
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateCaches();
                _cache.Remove($"newly_arrived_{id}");
            }

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var response = await _retryPolicy.ExecuteAsync(() =>
                _httpClient.DeleteAsync($"api/v1/newlyarrived/{id}")
            );

            if (response.IsSuccessStatusCode)
            {
                InvalidateCaches();
                _cache.Remove($"newly_arrived_{id}");
            }

            return response.IsSuccessStatusCode;
        }

        private void InvalidateCaches()
        {
            _logger.LogDebug("Invalidating newly arrived caches");
        }
    }
}