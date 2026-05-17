using AutoNext.Plotform.App.Backoffice.Models.Blob;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace AutoNext.Plotform.App.Backoffice.Integrations.Blob
{
    public class BlobService : IBlobService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<BlobService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

        private const string CLIENT_ID = "autonext_web_app";
        private const string CLIENT_SECRET = "a6cb4b9a-35a7-418d-bb8e-0c4a5527684b";

        public BlobService(
            HttpClient httpClient,
            ILogger<BlobService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _httpClient.Timeout = TimeSpan.FromSeconds(60);

            if (!_httpClient.DefaultRequestHeaders.Contains("X-Client-Id"))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Client-Id", CLIENT_ID);
            }

            if (!_httpClient.DefaultRequestHeaders.Contains("X-Client-Secret"))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Client-Secret", CLIENT_SECRET);
            }

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
                            "Retry {RetryCount} after {Delay}s for Blob API due to: {StatusCode}",
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

        public async Task<FileUploadResponseDto> UploadAsync(
            IFormFile file,
            Dictionary<string, string>? metadata = null)
        {
            try
            {
                _logger.LogInformation("Uploading file: {FileName}", file.FileName);

                using var content = new MultipartFormDataContent();
                using var stream = file.OpenReadStream();
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                content.Add(fileContent, "file", file.FileName);

                if (metadata != null)
                {
                    content.Add(
                        new StringContent(JsonConvert.SerializeObject(metadata), Encoding.UTF8, "application/json"),
                        "metadata");
                }

                var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync("api/v1/files/upload", content));
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic apiResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);
                var dataJson = apiResponse.data.ToString();

                return JsonConvert.DeserializeObject<FileUploadResponseDto>(dataJson) ?? new FileUploadResponseDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file: {FileName}", file.FileName);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(Guid fileId)
        {
            try
            {
                _logger.LogInformation("Deleting file: {FileId}", fileId);

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.DeleteAsync($"api/v1/files/{fileId}"));

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {FileId}", fileId);
                return false;
            }
        }

        public async Task<FileMetadata?> GetMetadataAsync(Guid fileId)
        {
            try
            {
                _logger.LogInformation("Getting metadata for file: {FileId}", fileId);

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/files/{fileId}"));

                if (!response.IsSuccessStatusCode)
                    return null;

                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic apiResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);
                var dataJson = apiResponse.data.ToString();

                return JsonConvert.DeserializeObject<FileMetadata>(dataJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting metadata for file: {FileId}", fileId);
                return null;
            }
        }

        public async Task<(Stream FileStream, string ContentType, string FileName)> DownloadAsync(Guid fileId)
        {
            try
            {
                _logger.LogInformation("Downloading file: {FileId}", fileId);

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/files/{fileId}/download"));

                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var fileName = response.Content.Headers.ContentDisposition?.FileName ?? fileId.ToString();

                return (stream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file: {FileId}", fileId);
                throw;
            }
        }

        public async Task<List<FileMetadata>> ListFilesAsync(int page = 1, int pageSize = 50)
        {
            try
            {
                _logger.LogInformation("Listing files");

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync($"api/v1/files?page={page}&pageSize={pageSize}"));

                if (!response.IsSuccessStatusCode)
                    return new List<FileMetadata>();

                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic apiResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);
                var dataJson = apiResponse.data.ToString();

                return JsonConvert.DeserializeObject<List<FileMetadata>>(dataJson) ?? new List<FileMetadata>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files");
                return new List<FileMetadata>();
            }
        }
    }
}