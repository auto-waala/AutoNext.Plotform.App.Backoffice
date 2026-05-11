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

namespace AutoNext.Plotform.App.Backoffice.Integrations.Core
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
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            outcome.Exception,
                            "Retry {RetryCount} after {Delay}s for NewlyArrived API due to: {StatusCode}",
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

        public async Task<PagedResult<NewlyArrivedResponseDto>> GetAllAsync(int page, int pageSize)
        {
            string cacheKey = $"newly_arrived_all_{page}_{pageSize}";

            if (_cache.TryGetValue(cacheKey, out PagedResult<NewlyArrivedResponseDto> cached))
            {
                _logger.LogDebug("Returning cached newly arrived list for page {Page}", page);
                return cached;
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cached))
                    return cached;

                _logger.LogInformation("Fetching newly arrived list from API - page {Page}, size {PageSize}", page, pageSize);

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all newly arrived", requestId);
                    return await _httpClient.GetAsync($"api/v1/newly-arrived?page={page}&pageSize={pageSize}");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get newly arrived list. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return new PagedResult<NewlyArrivedResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<PagedResult<NewlyArrivedResponseDto>>(content);

                if (result != null)
                {
                    _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                        SlidingExpiration = TimeSpan.FromMinutes(10)
                    });
                }

                return result ?? new PagedResult<NewlyArrivedResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching newly arrived list");
                return new PagedResult<NewlyArrivedResponseDto>();
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

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/newly-arrived/{id}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get newly arrived item {Id}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var item = JsonConvert.DeserializeObject<NewlyArrivedResponseDto>(content);

                if (item != null)
                {
                    _cache.Set(cacheKey, item, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60),
                        SlidingExpiration = TimeSpan.FromMinutes(20)
                    });
                }

                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching newly arrived item by ID: {Id}", id);
                return null;
            }
        }

        public async Task<NewlyArrivedResponseDto?> GetByModelSlugAsync(string modelSlug)
        {
            string cacheKey = $"newly_arrived_slug_{modelSlug}";

            if (_cache.TryGetValue(cacheKey, out NewlyArrivedResponseDto cached))
            {
                _logger.LogDebug("Returning cached newly arrived item for slug: {ModelSlug}", modelSlug);
                return cached;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/newly-arrived/slug/{modelSlug}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get newly arrived item by slug {ModelSlug}. Status: {StatusCode}", modelSlug, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var item = JsonConvert.DeserializeObject<NewlyArrivedResponseDto>(content);

                if (item != null)
                {
                    _cache.Set(cacheKey, item, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60),
                        SlidingExpiration = TimeSpan.FromMinutes(20)
                    });
                }

                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching newly arrived item by slug: {ModelSlug}", modelSlug);
                return null;
            }
        }

        public async Task<IEnumerable<NewlyArrivedResponseDto>> GetFeaturedArrivalsAsync(int limit = 10)
        {
            string cacheKey = $"newly_arrived_featured_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<NewlyArrivedResponseDto> cached))
            {
                _logger.LogDebug("Returning cached featured arrivals");
                return cached ?? Enumerable.Empty<NewlyArrivedResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/newly-arrived/featured?limit={limit}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get featured arrivals. Status: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<NewlyArrivedResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var items = JsonConvert.DeserializeObject<IEnumerable<NewlyArrivedResponseDto>>(content);

                if (items != null && items.Any())
                    _cache.Set(cacheKey, items, TimeSpan.FromMinutes(30));

                return items ?? Enumerable.Empty<NewlyArrivedResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching featured arrivals");
                return Enumerable.Empty<NewlyArrivedResponseDto>();
            }
        }

        public async Task<IEnumerable<NewlyArrivedResponseDto>> GetWeeklyArrivalsAsync(int limit = 20)
        {
            string cacheKey = $"newly_arrived_weekly_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<NewlyArrivedResponseDto> cached))
            {
                _logger.LogDebug("Returning cached weekly arrivals");
                return cached ?? Enumerable.Empty<NewlyArrivedResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/newly-arrived/weekly?limit={limit}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get weekly arrivals. Status: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<NewlyArrivedResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var items = JsonConvert.DeserializeObject<IEnumerable<NewlyArrivedResponseDto>>(content);

                if (items != null && items.Any())
                    _cache.Set(cacheKey, items, TimeSpan.FromMinutes(60));

                return items ?? Enumerable.Empty<NewlyArrivedResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching weekly arrivals");
                return Enumerable.Empty<NewlyArrivedResponseDto>();
            }
        }

        public async Task<IEnumerable<NewlyArrivedResponseDto>> GetMonthlyArrivalsAsync(int month, int year, int limit = 20)
        {
            string cacheKey = $"newly_arrived_monthly_{month}_{year}_{limit}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<NewlyArrivedResponseDto> cached))
            {
                _logger.LogDebug("Returning cached monthly arrivals for {Month}/{Year}", month, year);
                return cached ?? Enumerable.Empty<NewlyArrivedResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/newly-arrived/monthly?month={month}&year={year}&limit={limit}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get monthly arrivals for {Month}/{Year}. Status: {StatusCode}", month, year, response.StatusCode);
                    return Enumerable.Empty<NewlyArrivedResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var items = JsonConvert.DeserializeObject<IEnumerable<NewlyArrivedResponseDto>>(content);

                if (items != null && items.Any())
                    _cache.Set(cacheKey, items, TimeSpan.FromMinutes(60));

                return items ?? Enumerable.Empty<NewlyArrivedResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching monthly arrivals for {Month}/{Year}", month, year);
                return Enumerable.Empty<NewlyArrivedResponseDto>();
            }
        }

        public async Task<IEnumerable<NewlyArrivedResponseDto>> GetYearlyArrivalsAsync(int year)
        {
            string cacheKey = $"newly_arrived_yearly_{year}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<NewlyArrivedResponseDto> cached))
            {
                _logger.LogDebug("Returning cached yearly arrivals for {Year}", year);
                return cached ?? Enumerable.Empty<NewlyArrivedResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/newly-arrived/yearly?year={year}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get yearly arrivals for {Year}. Status: {StatusCode}", year, response.StatusCode);
                    return Enumerable.Empty<NewlyArrivedResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var items = JsonConvert.DeserializeObject<IEnumerable<NewlyArrivedResponseDto>>(content);

                if (items != null && items.Any())
                    _cache.Set(cacheKey, items, TimeSpan.FromMinutes(60));

                return items ?? Enumerable.Empty<NewlyArrivedResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching yearly arrivals for {Year}", year);
                return Enumerable.Empty<NewlyArrivedResponseDto>();
            }
        }

        public async Task<NewlyArrivedResponseDto> CreateAsync(NewlyArrivedRequestDto request, string publishedBy)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/newly-arrived", request));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("A newly arrived entry with these details already exists");

                response.EnsureSuccessStatusCode();

                InvalidateNewlyArrivedCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<NewlyArrivedResponseDto>(content)!;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating newly arrived item");
                throw new InvalidOperationException("Failed to create newly arrived item. Please try again.", ex);
            }
        }

        public async Task<NewlyArrivedResponseDto?> UpdateAsync(string id, NewlyArrivedRequestDto request)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/newly-arrived/{id}", request));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to update newly arrived item {Id}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                InvalidateNewlyArrivedCaches();
                _cache.Remove($"newly_arrived_{id}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<NewlyArrivedResponseDto>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating newly arrived item: {Id}", id);
                return null;
            }
        }

        public async Task<bool> PublishAsync(string id, string publishedBy)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/newly-arrived/{id}/publish", JsonContent.Create(new { publishedBy })));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateNewlyArrivedCaches();
                    _cache.Remove($"newly_arrived_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing newly arrived item: {Id}", id);
                return false;
            }
        }

        public async Task<bool> UnpublishAsync(string id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/newly-arrived/{id}/unpublish", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateNewlyArrivedCaches();
                    _cache.Remove($"newly_arrived_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpublishing newly arrived item: {Id}", id);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(string id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/newly-arrived/{id}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateNewlyArrivedCaches();
                    _cache.Remove($"newly_arrived_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting newly arrived item: {Id}", id);
                return false;
            }
        }

        private void InvalidateNewlyArrivedCaches()
        {
            _cache.Remove("newly_arrived_all");
            _logger.LogDebug("Newly arrived caches invalidated");
        }
    }
}