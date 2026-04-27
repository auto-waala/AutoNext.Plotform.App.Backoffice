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
    public class DocumentTypeService : IDocumentTypeService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DocumentTypeService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public DocumentTypeService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<DocumentTypeService> logger)
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
                            "Retry {RetryCount} after {Delay}s for DocumentType API due to: {StatusCode}",
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

        public async Task<IEnumerable<DocumentTypeResponseDto>> GetAllDocumentTypesAsync()
        {
            const string cacheKey = "all_document_types";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<DocumentTypeResponseDto> cachedTypes))
            {
                _logger.LogDebug("Returning cached document types");
                return cachedTypes ?? Enumerable.Empty<DocumentTypeResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedTypes))
                {
                    return cachedTypes ?? Enumerable.Empty<DocumentTypeResponseDto>();
                }

                _logger.LogInformation("Fetching all document types from API");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all document types", requestId);
                    return await _httpClient.GetAsync("api/v1/documenttypes");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get document types. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<DocumentTypeResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var types = JsonConvert.DeserializeObject<IEnumerable<DocumentTypeResponseDto>>(content);

                if (types != null && types.Any())
                {
                    _cache.Set(cacheKey, types, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }

                return types ?? Enumerable.Empty<DocumentTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching document types");
                return Enumerable.Empty<DocumentTypeResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<IEnumerable<DocumentTypeResponseDto>> GetActiveDocumentTypesAsync()
        {
            const string cacheKey = "active_document_types";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<DocumentTypeResponseDto> cachedTypes))
            {
                return cachedTypes ?? Enumerable.Empty<DocumentTypeResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/documenttypes/active"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<DocumentTypeResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var types = JsonConvert.DeserializeObject<IEnumerable<DocumentTypeResponseDto>>(content);

                if (types != null && types.Any())
                {
                    _cache.Set(cacheKey, types, TimeSpan.FromMinutes(10));
                }

                return types ?? Enumerable.Empty<DocumentTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active document types");
                return Enumerable.Empty<DocumentTypeResponseDto>();
            }
        }

        public async Task<IEnumerable<DocumentTypeResponseDto>> GetRequiredDocumentTypesAsync()
        {
            const string cacheKey = "required_document_types";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<DocumentTypeResponseDto> cachedTypes))
            {
                return cachedTypes ?? Enumerable.Empty<DocumentTypeResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/documenttypes/required"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<DocumentTypeResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var types = JsonConvert.DeserializeObject<IEnumerable<DocumentTypeResponseDto>>(content);

                if (types != null && types.Any())
                {
                    _cache.Set(cacheKey, types, TimeSpan.FromMinutes(10));
                }

                return types ?? Enumerable.Empty<DocumentTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching required document types");
                return Enumerable.Empty<DocumentTypeResponseDto>();
            }
        }

        public async Task<IEnumerable<DocumentTypeResponseDto>> GetVerifiableDocumentTypesAsync()
        {
            const string cacheKey = "verifiable_document_types";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<DocumentTypeResponseDto> cachedTypes))
            {
                return cachedTypes ?? Enumerable.Empty<DocumentTypeResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/documenttypes/verifiable"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<DocumentTypeResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var types = JsonConvert.DeserializeObject<IEnumerable<DocumentTypeResponseDto>>(content);

                if (types != null && types.Any())
                {
                    _cache.Set(cacheKey, types, TimeSpan.FromMinutes(10));
                }

                return types ?? Enumerable.Empty<DocumentTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching verifiable document types");
                return Enumerable.Empty<DocumentTypeResponseDto>();
            }
        }

        public async Task<DocumentTypeResponseDto?> GetDocumentTypeByIdAsync(Guid documentTypeId)
        {
            string cacheKey = $"document_type_{documentTypeId}";

            if (_cache.TryGetValue(cacheKey, out DocumentTypeResponseDto cachedType))
            {
                return cachedType;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/documenttypes/{documentTypeId}"));

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var type = JsonConvert.DeserializeObject<DocumentTypeResponseDto>(content);

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
                _logger.LogError(ex, "Error fetching document type by ID: {DocumentTypeId}", documentTypeId);
                return null;
            }
        }

        public async Task<DocumentTypeResponseDto?> GetDocumentTypeByCodeAsync(string code)
        {
            string cacheKey = $"document_type_code_{code}";

            if (_cache.TryGetValue(cacheKey, out DocumentTypeResponseDto cachedType))
            {
                return cachedType;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/documenttypes/code/{code}"));

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var type = JsonConvert.DeserializeObject<DocumentTypeResponseDto>(content);

                if (type != null)
                {
                    _cache.Set(cacheKey, type, TimeSpan.FromMinutes(15));
                }

                return type;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching document type by code: {Code}", code);
                return null;
            }
        }

        public async Task<IEnumerable<DocumentTypeResponseDto>> GetDocumentTypesByCategoryAsync(string category)
        {
            string cacheKey = $"document_types_category_{category}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<DocumentTypeResponseDto> cachedTypes))
            {
                return cachedTypes ?? Enumerable.Empty<DocumentTypeResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/documenttypes/category/{category}"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<DocumentTypeResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var types = JsonConvert.DeserializeObject<IEnumerable<DocumentTypeResponseDto>>(content);

                if (types != null && types.Any())
                {
                    _cache.Set(cacheKey, types, TimeSpan.FromMinutes(15));
                }

                return types ?? Enumerable.Empty<DocumentTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching document types by category: {Category}", category);
                return Enumerable.Empty<DocumentTypeResponseDto>();
            }
        }

        public async Task<IEnumerable<DocumentTypeResponseDto>> GetDocumentTypesByVehicleTypeAsync(string vehicleType)
        {
            string cacheKey = $"document_types_vehicle_{vehicleType}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<DocumentTypeResponseDto> cachedTypes))
            {
                return cachedTypes ?? Enumerable.Empty<DocumentTypeResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/documenttypes/vehicle-type/{vehicleType}"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<DocumentTypeResponseDto>();

                var content = await response.Content.ReadAsStringAsync();
                var types = JsonConvert.DeserializeObject<IEnumerable<DocumentTypeResponseDto>>(content);

                if (types != null && types.Any())
                {
                    _cache.Set(cacheKey, types, TimeSpan.FromMinutes(15));
                }

                return types ?? Enumerable.Empty<DocumentTypeResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching document types by vehicle type: {VehicleType}", vehicleType);
                return Enumerable.Empty<DocumentTypeResponseDto>();
            }
        }

        public async Task<IEnumerable<string>> GetDistinctCategoriesAsync()
        {
            const string cacheKey = "document_type_categories";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<string> cachedCategories))
            {
                return cachedCategories ?? Enumerable.Empty<string>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/documenttypes/categories"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<string>();

                var content = await response.Content.ReadAsStringAsync();
                var categories = JsonConvert.DeserializeObject<IEnumerable<string>>(content);

                if (categories != null && categories.Any())
                {
                    _cache.Set(cacheKey, categories, TimeSpan.FromMinutes(30));
                }

                return categories ?? Enumerable.Empty<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching distinct document type categories");
                return Enumerable.Empty<string>();
            }
        }

        public async Task<IEnumerable<DocumentTypeCategoryDto>> GetDocumentTypesGroupedByCategoryAsync()
        {
            const string cacheKey = "document_types_grouped";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<DocumentTypeCategoryDto> cachedGrouped))
            {
                return cachedGrouped ?? Enumerable.Empty<DocumentTypeCategoryDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync("api/v1/documenttypes/grouped"));

                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<DocumentTypeCategoryDto>();

                var content = await response.Content.ReadAsStringAsync();
                var grouped = JsonConvert.DeserializeObject<IEnumerable<DocumentTypeCategoryDto>>(content);

                if (grouped != null && grouped.Any())
                {
                    _cache.Set(cacheKey, grouped, TimeSpan.FromMinutes(10));
                }

                return grouped ?? Enumerable.Empty<DocumentTypeCategoryDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching document types grouped by category");
                return Enumerable.Empty<DocumentTypeCategoryDto>();
            }
        }

        public async Task<DocumentTypeResponseDto> CreateDocumentTypeAsync(DocumentTypeCreateDto createDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/documenttypes", createDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Document type with this code or name already exists");

                response.EnsureSuccessStatusCode();

                InvalidateDocumentTypeCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<DocumentTypeResponseDto>(content)!;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating document type");
                throw new InvalidOperationException("Failed to create document type. Please try again.", ex);
            }
        }

        public async Task<DocumentTypeResponseDto?> UpdateDocumentTypeAsync(Guid documentTypeId, DocumentTypeUpdateDto updateDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PutAsJsonAsync($"api/v1/documenttypes/{documentTypeId}", updateDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Duplicate document type code or name");

                if (!response.IsSuccessStatusCode)
                    return null;

                InvalidateDocumentTypeCaches();
                _cache.Remove($"document_type_{documentTypeId}");

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<DocumentTypeResponseDto>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document type: {DocumentTypeId}", documentTypeId);
                return null;
            }
        }

        public async Task<bool> ToggleDocumentTypeStatusAsync(Guid documentTypeId, bool isActive)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PatchAsync($"api/v1/documenttypes/{documentTypeId}/toggle/{isActive}", null));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateDocumentTypeCaches();
                    _cache.Remove($"document_type_{documentTypeId}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling document type status: {DocumentTypeId}", documentTypeId);
                return false;
            }
        }

        public async Task<bool> ReorderDocumentTypesAsync(Dictionary<Guid, int> orderMap)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/documenttypes/reorder", orderMap));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateDocumentTypeCaches();
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering document types");
                return false;
            }
        }

        public async Task<bool> DeleteDocumentTypeAsync(Guid documentTypeId)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/documenttypes/{documentTypeId}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateDocumentTypeCaches();
                    _cache.Remove($"document_type_{documentTypeId}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document type: {DocumentTypeId}", documentTypeId);
                return false;
            }
        }

        private void InvalidateDocumentTypeCaches()
        {
            _cache.Remove("all_document_types");
            _cache.Remove("active_document_types");
            _cache.Remove("required_document_types");
            _cache.Remove("verifiable_document_types");
            _cache.Remove("document_type_categories");
            _cache.Remove("document_types_grouped");
            _logger.LogDebug("Document type caches invalidated");
        }
    }
}