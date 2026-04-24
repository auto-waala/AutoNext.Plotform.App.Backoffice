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
    public class CategoryService : ICategoryService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CategoryService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public CategoryService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<CategoryService> logger)
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
                            "Retry {RetryCount} after {Delay}s for Category API due to: {StatusCode}",
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

        public async Task<IEnumerable<CategoryResponseDto>> GetAllCategoriesAsync()
        {
            const string cacheKey = "all_categories";

            // Try to get from cache first
            if (_cache.TryGetValue(cacheKey, out IEnumerable<CategoryResponseDto> cachedCategories))
            {
                _logger.LogDebug("Returning cached categories");
                return cachedCategories ?? Enumerable.Empty<CategoryResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                // Double-check cache after acquiring lock
                if (_cache.TryGetValue(cacheKey, out cachedCategories))
                {
                    return cachedCategories ?? Enumerable.Empty<CategoryResponseDto>();
                }

                _logger.LogInformation("Fetching all categories from API");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all categories", requestId);
                    return await _httpClient.GetAsync("api/v1/categories");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get categories. Status: {StatusCode}", response.StatusCode);

                    // Log error details for 500 errors
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }

                    return Enumerable.Empty<CategoryResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var categories = JsonConvert.DeserializeObject<IEnumerable<CategoryResponseDto>>(content);

                // Cache for 5 minutes
                if (categories != null && categories.Any())
                {
                    _cache.Set(cacheKey, categories, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    });
                }

                return categories ?? Enumerable.Empty<CategoryResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching categories");
                return Enumerable.Empty<CategoryResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<IEnumerable<CategoryResponseDto>> GetActiveCategoriesAsync()
        {
            const string cacheKey = "active_categories";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<CategoryResponseDto> cachedCategories))
            {
                return cachedCategories ?? Enumerable.Empty<CategoryResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/categories/active"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<CategoryResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var categories = JsonConvert.DeserializeObject<IEnumerable<CategoryResponseDto>>(content);

                if (categories != null && categories.Any())
                {
                    _cache.Set(cacheKey, categories, TimeSpan.FromMinutes(5));
                }

                return categories ?? Enumerable.Empty<CategoryResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active categories");
                return Enumerable.Empty<CategoryResponseDto>();
            }
        }

        public async Task<IEnumerable<CategoryResponseDto>> GetMainCategoriesAsync()
        {
            const string cacheKey = "main_categories";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<CategoryResponseDto> cachedCategories))
            {
                return cachedCategories ?? Enumerable.Empty<CategoryResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/categories/main"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<CategoryResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var categories = JsonConvert.DeserializeObject<IEnumerable<CategoryResponseDto>>(content);

                if (categories != null && categories.Any())
                {
                    _cache.Set(cacheKey, categories, TimeSpan.FromMinutes(5));
                }

                return categories ?? Enumerable.Empty<CategoryResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching main categories");
                return Enumerable.Empty<CategoryResponseDto>();
            }
        }

        public async Task<IEnumerable<CategoryTreeDto>> GetCategoryTreeAsync()
        {
            const string cacheKey = "category_tree";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<CategoryTreeDto> cachedTree))
            {
                return cachedTree ?? Enumerable.Empty<CategoryTreeDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/categories/tree"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<CategoryTreeDto>();

                var content = await response.Content.ReadAsStringAsync();
                var tree = JsonConvert.DeserializeObject<IEnumerable<CategoryTreeDto>>(content);

                if (tree != null && tree.Any())
                {
                    _cache.Set(cacheKey, tree, TimeSpan.FromMinutes(10));
                }

                return tree ?? Enumerable.Empty<CategoryTreeDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching category tree");
                return Enumerable.Empty<CategoryTreeDto>();
            }
        }

        public async Task<CategoryResponseDto?> GetCategoryByIdAsync(Guid categoryId)
        {
            string cacheKey = $"category_{categoryId}";

            if (_cache.TryGetValue(cacheKey, out CategoryResponseDto cachedCategory))
            {
                return cachedCategory;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/categories/{categoryId}"));

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var category = JsonConvert.DeserializeObject<CategoryResponseDto>(content);

                if (category != null)
                {
                    _cache.Set(cacheKey, category, TimeSpan.FromMinutes(10));
                }

                return category;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching category by ID: {CategoryId}", categoryId);
                return null;
            }
        }

        public async Task<CategoryResponseDto?> GetCategoryByCodeAsync(string code)
        {
            string cacheKey = $"category_code_{code}";

            if (_cache.TryGetValue(cacheKey, out CategoryResponseDto cachedCategory))
            {
                return cachedCategory;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/categories/code/{code}"));

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var category = JsonConvert.DeserializeObject<CategoryResponseDto>(content);

                if (category != null)
                {
                    _cache.Set(cacheKey, category, TimeSpan.FromMinutes(10));
                }

                return category;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching category by code: {Code}", code);
                return null;
            }
        }

        public async Task<CategoryResponseDto?> GetCategoryBySlugAsync(string slug)
        {
            string cacheKey = $"category_slug_{slug}";

            if (_cache.TryGetValue(cacheKey, out CategoryResponseDto cachedCategory))
            {
                return cachedCategory;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/categories/slug/{slug}"));

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var category = JsonConvert.DeserializeObject<CategoryResponseDto>(content);

                if (category != null)
                {
                    _cache.Set(cacheKey, category, TimeSpan.FromMinutes(10));
                }

                return category;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching category by slug: {Slug}", slug);
                return null;
            }
        }

        public async Task<IEnumerable<CategoryResponseDto>> GetSubCategoriesAsync(Guid parentCategoryId)
        {
            string cacheKey = $"subcategories_{parentCategoryId}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<CategoryResponseDto> cachedCategories))
            {
                return cachedCategories ?? Enumerable.Empty<CategoryResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/categories/{parentCategoryId}/subcategories"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<CategoryResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var categories = JsonConvert.DeserializeObject<IEnumerable<CategoryResponseDto>>(content);

                if (categories != null && categories.Any())
                {
                    _cache.Set(cacheKey, categories, TimeSpan.FromMinutes(5));
                }

                return categories ?? Enumerable.Empty<CategoryResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching subcategories for parent: {ParentCategoryId}", parentCategoryId);
                return Enumerable.Empty<CategoryResponseDto>();
            }
        }

        public async Task<CategoryResponseDto> CreateCategoryAsync(CategoryCreateDto createDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/categories", createDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Category already exists");

                response.EnsureSuccessStatusCode();

                // Invalidate cache after create
                InvalidateCategoryCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<CategoryResponseDto>(content)!;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating category");
                throw new InvalidOperationException("Failed to create category. Please try again.", ex);
            }
        }

        public async Task<CategoryResponseDto?> UpdateCategoryAsync(Guid categoryId, CategoryUpdateDto updateDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/categories/{categoryId}", updateDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate category code or slug");

                if (!response.IsSuccessStatusCode)
                    return null;

                // Invalidate cache after update
                InvalidateCategoryCaches();
                _cache.Remove($"category_{categoryId}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<CategoryResponseDto>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category: {CategoryId}", categoryId);
                return null;
            }
        }

        public async Task<bool> ToggleCategoryStatusAsync(Guid categoryId, bool isActive)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync(
                        $"api/v1/categories/{categoryId}/toggle/{isActive}",
                        null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateCategoryCaches();
                    _cache.Remove($"category_{categoryId}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling category status: {CategoryId}", categoryId);
                return false;
            }
        }

        public async Task<bool> ReorderCategoriesAsync(Dictionary<Guid, int> orderMap)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/categories/reorder", orderMap));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateCategoryCaches();
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering categories");
                return false;
            }
        }

        public async Task<bool> DeleteCategoryAsync(Guid categoryId)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/categories/{categoryId}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateCategoryCaches();
                    _cache.Remove($"category_{categoryId}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category: {CategoryId}", categoryId);
                return false;
            }
        }

        private void InvalidateCategoryCaches()
        {
            _cache.Remove("all_categories");
            _cache.Remove("active_categories");
            _cache.Remove("main_categories");
            _cache.Remove("category_tree");
            // Remove all subcategory caches (if needed)
            // Note: In a production environment, you might want more sophisticated cache invalidation
        }
    }
}