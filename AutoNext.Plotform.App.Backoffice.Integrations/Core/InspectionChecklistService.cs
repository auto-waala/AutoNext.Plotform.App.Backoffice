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
    public class InspectionChecklistService : IInspectionChecklistService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<InspectionChecklistService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public InspectionChecklistService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<InspectionChecklistService> logger)
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
                            "Retry {RetryCount} after {Delay}s for InspectionChecklist API due to: {StatusCode}",
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

        public async Task<IEnumerable<InspectionChecklistResponseDto>> GetAllAsync()
        {
            const string cacheKey = "all_inspection_checklists";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<InspectionChecklistResponseDto> cachedChecklists))
            {
                _logger.LogDebug("Returning cached inspection checklists");
                return cachedChecklists ?? Enumerable.Empty<InspectionChecklistResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedChecklists))
                {
                    return cachedChecklists ?? Enumerable.Empty<InspectionChecklistResponseDto>();
                }

                _logger.LogInformation("Fetching all inspection checklists from API");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all inspection checklists", requestId);
                    return await _httpClient.GetAsync("api/v1/inspection-checklist");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get inspection checklists. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<InspectionChecklistResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var checklists = JsonConvert.DeserializeObject<IEnumerable<InspectionChecklistResponseDto>>(content);

                if (checklists != null && checklists.Any())
                {
                    _cache.Set(cacheKey, checklists, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return checklists ?? Enumerable.Empty<InspectionChecklistResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching inspection checklists");
                return Enumerable.Empty<InspectionChecklistResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<IEnumerable<InspectionChecklistResponseDto>> GetActiveAsync()
        {
            const string cacheKey = "active_inspection_checklists";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<InspectionChecklistResponseDto> cachedChecklists))
            {
                _logger.LogDebug("Returning cached active inspection checklists");
                return cachedChecklists ?? Enumerable.Empty<InspectionChecklistResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/inspection-checklist/active"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get active inspection checklists. Status: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<InspectionChecklistResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var checklists = JsonConvert.DeserializeObject<IEnumerable<InspectionChecklistResponseDto>>(content);

                if (checklists != null && checklists.Any())
                {
                    _cache.Set(cacheKey, checklists, TimeSpan.FromMinutes(10));
                }

                return checklists ?? Enumerable.Empty<InspectionChecklistResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active inspection checklists");
                return Enumerable.Empty<InspectionChecklistResponseDto>();
            }
        }

        public async Task<InspectionChecklistResponseDto?> GetByIdAsync(Guid id)
        {
            string cacheKey = $"inspection_checklist_{id}";

            if (_cache.TryGetValue(cacheKey, out InspectionChecklistResponseDto cachedChecklist))
            {
                return cachedChecklist;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/inspection-checklist/{id}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get inspection checklist {ChecklistId}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var checklist = JsonConvert.DeserializeObject<InspectionChecklistResponseDto>(content);

                if (checklist != null)
                {
                    _cache.Set(cacheKey, checklist, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return checklist;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching inspection checklist by ID: {ChecklistId}", id);
                return null;
            }
        }

        public async Task<IEnumerable<InspectionChecklistResponseDto>> GetByCategoryAsync(string category)
        {
            string cacheKey = $"inspection_checklists_category_{category}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<InspectionChecklistResponseDto> cachedChecklists))
            {
                _logger.LogDebug("Returning cached inspection checklists for category: {Category}", category);
                return cachedChecklists ?? Enumerable.Empty<InspectionChecklistResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/inspection-checklist/category/{category}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get inspection checklists by category {Category}. Status: {StatusCode}", category, response.StatusCode);
                    return Enumerable.Empty<InspectionChecklistResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var checklists = JsonConvert.DeserializeObject<IEnumerable<InspectionChecklistResponseDto>>(content);

                if (checklists != null && checklists.Any())
                {
                    _cache.Set(cacheKey, checklists, TimeSpan.FromMinutes(15));
                }

                return checklists ?? Enumerable.Empty<InspectionChecklistResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching inspection checklists by category: {Category}", category);
                return Enumerable.Empty<InspectionChecklistResponseDto>();
            }
        }

        public async Task<InspectionChecklistResponseDto> CreateAsync(InspectionChecklistCreateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/inspection-checklist", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Inspection checklist with this code or name already exists");

                response.EnsureSuccessStatusCode();

                // Invalidate caches after create
                InvalidateChecklistCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<InspectionChecklistResponseDto>(content)!;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating inspection checklist");
                throw new InvalidOperationException("Failed to create inspection checklist. Please try again.", ex);
            }
        }

        public async Task<InspectionChecklistResponseDto?> UpdateAsync(Guid id, InspectionChecklistUpdateDto dto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/inspection-checklist/{id}", dto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate inspection checklist code or name");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Update failed for inspection checklist {ChecklistId}. Status: {StatusCode}", id, response.StatusCode);
                    return null;
                }

                // Invalidate caches after update
                InvalidateChecklistCaches();
                _cache.Remove($"inspection_checklist_{id}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<InspectionChecklistResponseDto>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating inspection checklist: {ChecklistId}", id);
                return null;
            }
        }

        public async Task<bool> ToggleStatusAsync(Guid id, bool isActive)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/inspection-checklist/{id}/toggle/{isActive}", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateChecklistCaches();
                    _cache.Remove($"inspection_checklist_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling inspection checklist status: {ChecklistId}", id);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/inspection-checklist/{id}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateChecklistCaches();
                    _cache.Remove($"inspection_checklist_{id}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting inspection checklist: {ChecklistId}", id);
                return false;
            }
        }

        private void InvalidateChecklistCaches()
        {
            _cache.Remove("all_inspection_checklists");
            _cache.Remove("active_inspection_checklists");
            // Category-specific caches preserved to avoid over-invalidation
            _logger.LogDebug("Inspection checklist caches invalidated");
        }
    }
}