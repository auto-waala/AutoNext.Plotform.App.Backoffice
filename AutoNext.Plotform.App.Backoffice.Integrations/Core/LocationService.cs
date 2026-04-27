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
    public class LocationService : ILocationService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<LocationService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public LocationService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<LocationService> logger)
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
                            "Retry {RetryCount} after {Delay}s for Location API due to: {StatusCode}",
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

        public async Task<IEnumerable<LocationResponseDto>> GetAllLocationsAsync()
        {
            const string cacheKey = "all_locations";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<LocationResponseDto> cachedLocations))
            {
                _logger.LogDebug("Returning cached locations");
                return cachedLocations ?? Enumerable.Empty<LocationResponseDto>();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedLocations))
                {
                    return cachedLocations ?? Enumerable.Empty<LocationResponseDto>();
                }

                _logger.LogInformation("Fetching all locations from API");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var requestId = Guid.NewGuid();
                    _logger.LogDebug("[{RequestId}] Sending request to get all locations", requestId);
                    return await _httpClient.GetAsync("api/v1/locations");
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get locations. Status: {StatusCode}", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("500 Error response: {ErrorContent}", errorContent);
                    }
                    return Enumerable.Empty<LocationResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var locations = JsonConvert.DeserializeObject<IEnumerable<LocationResponseDto>>(content);

                if (locations != null && locations.Any())
                {
                    _cache.Set(cacheKey, locations, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                        SlidingExpiration = TimeSpan.FromMinutes(10)
                    });
                }

                return locations ?? Enumerable.Empty<LocationResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching locations");
                return Enumerable.Empty<LocationResponseDto>();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task<LocationResponseDto?> GetLocationByIdAsync(Guid locationId)
        {
            string cacheKey = $"location_{locationId}";

            if (_cache.TryGetValue(cacheKey, out LocationResponseDto cachedLocation))
            {
                return cachedLocation;
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/locations/{locationId}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get location {LocationId}. Status: {StatusCode}", locationId, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var location = JsonConvert.DeserializeObject<LocationResponseDto>(content);

                if (location != null)
                {
                    _cache.Set(cacheKey, location, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60),
                        SlidingExpiration = TimeSpan.FromMinutes(20)
                    });
                }

                return location;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching location by ID: {LocationId}", locationId);
                return null;
            }
        }

        public async Task<IEnumerable<LocationResponseDto>> GetLocationsByStateAsync(string stateCode)
        {
            string cacheKey = $"locations_state_{stateCode}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<LocationResponseDto> cachedLocations))
            {
                _logger.LogDebug("Returning cached locations for state: {StateCode}", stateCode);
                return cachedLocations ?? Enumerable.Empty<LocationResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/locations/state/{stateCode}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get locations by state {StateCode}. Status: {StatusCode}", stateCode, response.StatusCode);
                    return Enumerable.Empty<LocationResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var locations = JsonConvert.DeserializeObject<IEnumerable<LocationResponseDto>>(content);

                if (locations != null && locations.Any())
                {
                    _cache.Set(cacheKey, locations, TimeSpan.FromMinutes(60));
                }

                return locations ?? Enumerable.Empty<LocationResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching locations by state: {StateCode}", stateCode);
                return Enumerable.Empty<LocationResponseDto>();
            }
        }

        public async Task<IEnumerable<LocationResponseDto>> GetLocationsByCityAsync(string cityName)
        {
            string cacheKey = $"locations_city_{cityName}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<LocationResponseDto> cachedLocations))
            {
                _logger.LogDebug("Returning cached locations for city: {CityName}", cityName);
                return cachedLocations ?? Enumerable.Empty<LocationResponseDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/locations/city/{cityName}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get locations by city {CityName}. Status: {StatusCode}", cityName, response.StatusCode);
                    return Enumerable.Empty<LocationResponseDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var locations = JsonConvert.DeserializeObject<IEnumerable<LocationResponseDto>>(content);

                if (locations != null && locations.Any())
                {
                    _cache.Set(cacheKey, locations, TimeSpan.FromMinutes(60));
                }

                return locations ?? Enumerable.Empty<LocationResponseDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching locations by city: {CityName}", cityName);
                return Enumerable.Empty<LocationResponseDto>();
            }
        }

        public async Task<IEnumerable<CityAreaDto>> GetAreasByCityAsync(Guid locationId)
        {
            string cacheKey = $"location_areas_{locationId}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<CityAreaDto> cachedAreas))
            {
                _logger.LogDebug("Returning cached areas for location: {LocationId}", locationId);
                return cachedAreas ?? Enumerable.Empty<CityAreaDto>();
            }

            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/locations/{locationId}/areas"));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get areas for location {LocationId}. Status: {StatusCode}", locationId, response.StatusCode);
                    return Enumerable.Empty<CityAreaDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var areas = JsonConvert.DeserializeObject<IEnumerable<CityAreaDto>>(content);

                if (areas != null && areas.Any())
                {
                    _cache.Set(cacheKey, areas, TimeSpan.FromMinutes(60));
                }

                return areas ?? Enumerable.Empty<CityAreaDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching areas by location: {LocationId}", locationId);
                return Enumerable.Empty<CityAreaDto>();
            }
        }

        public async Task<LocationResponseDto> CreateLocationAsync(LocationCreateDto createDto)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/locations", createDto));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("Location with these coordinates or details already exists");

                response.EnsureSuccessStatusCode();

                // Invalidate caches after create
                InvalidateLocationCaches();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<LocationResponseDto>(content)!;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error creating location");
                throw new InvalidOperationException("Failed to create location. Please try again.", ex);
            }
        }

        public async Task<bool> DeleteLocationAsync(Guid locationId)
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/locations/{locationId}"));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateLocationCaches();
                    _cache.Remove($"location_{locationId}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting location: {LocationId}", locationId);
                return false;
            }
        }

        private void InvalidateLocationCaches()
        {
            _cache.Remove("all_locations");
            // Note: Geographic caches (state/city) are not invalidated here
            // as it would be too broad. Consider using cache tags for production
            _logger.LogDebug("Location caches invalidated");
        }
    }
}