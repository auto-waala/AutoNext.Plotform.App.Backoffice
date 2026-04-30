using AutoNext.Plotform.App.Backoffice.Models.Common;
using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using System.Net;
using System.Net.Http.Json;

namespace AutoNext.Plotform.App.Backoffice.Integrations.AccessControl
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AuthService> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

        public AuthService(HttpClient httpClient, IMemoryCache memoryCache, ILogger<AuthService> logger)
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
                            "Retry {RetryCount} after {Delay}s for Auth API due to: {StatusCode}",
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

        public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
        {
            try
            {
                _logger.LogInformation("Login attempt for user: {Email}", request.Email);

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/auth/login", request));

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Invalid login attempt for: {Email}", request.Email);
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Login failed with status: {StatusCode}", response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();

                var authResponse = JsonConvert.DeserializeObject<ApiResponse<AuthResponseDto>>(content);

                return authResponse.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for: {Email}", request.Email);
                return null;
            }
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterUserDto request)
        {
            try
            {
                _logger.LogInformation("Registering new user: {Email}", request.Email);

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/auth/register", request));

                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new InvalidOperationException("User with this email already exists");

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Registration failed: {errorContent}");
                }

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var authResponse = JsonConvert.DeserializeObject<AuthResponseDto>(content);

                if (authResponse == null)
                    throw new InvalidOperationException("Failed to parse registration response");

                return authResponse;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error during registration for: {Email}", request.Email);
                throw new InvalidOperationException("Failed to register user. Please try again.", ex);
            }
        }

        public async Task<AuthResponseDto?> GoogleLoginAsync(GoogleLoginRequestDto request)
        {
            try
            {
                _logger.LogInformation("Google login attempt");

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/auth/google", request));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Google login failed with status: {StatusCode}", response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<AuthResponseDto>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Google login");
                return null;
            }
        }

        public async Task<AuthResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto request)
        {
            try
            {
                _logger.LogInformation("Refreshing token");

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/auth/refresh", request));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Refresh token failed with status: {StatusCode}", response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<AuthResponseDto>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return null;
            }
        }

        public async Task<bool> LogoutAsync(Guid userId, string refreshToken)
        {
            try
            {
                _logger.LogInformation("Logout for user: {UserId}", userId);

                var request = new { refreshToken };
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/auth/logout", request));

                if (response.IsSuccessStatusCode)
                {
                    InvalidateUserCaches(userId);
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto request)
        {
            try
            {
                _logger.LogInformation("Changing password for user: {UserId}", userId);

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync($"api/v1/auth/change-password", request));

                if (!response.IsSuccessStatusCode && response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Password change failed: {Error}", errorContent);
                    return false;
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ForgotPasswordAsync(ForgotPasswordDto request)
        {
            try
            {
                _logger.LogInformation("Forgot password request for email: {Email}", request.Email);

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/auth/forgot-password", request));

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during forgot password for: {Email}", request.Email);
                return false;
            }
        }

        public async Task<bool> ResetPasswordAsync(ResetPasswordRequestDto request)
        {
            try
            {
                _logger.LogInformation("Resetting password");

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/auth/reset-password", request));

                if (!response.IsSuccessStatusCode && response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Password reset failed: {errorContent}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset");
                throw;
            }
        }

        public async Task<bool> SendVerificationOtpAsync(string email, string purpose)
        {
            try
            {
                _logger.LogInformation("Sending verification OTP to: {Email} for purpose: {Purpose}", email, purpose);

                var request = new { email, purpose };
                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/auth/send-verification-otp", request));

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending verification OTP to: {Email}", email);
                return false;
            }
        }

        public async Task<bool> VerifyOtpAsync(VerifyOtpRequestDto request)
        {
            try
            {
                _logger.LogInformation("Verifying OTP for: {Email}", request.Email);

                var response = await _retryPolicy.ExecuteAsync(() =>
                    _httpClient.PostAsJsonAsync("api/v1/auth/verify-otp", request));

                if (!response.IsSuccessStatusCode && response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"OTP verification failed: {errorContent}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP for: {Email}", request.Email);
                throw;
            }
        }

        private void InvalidateUserCaches(Guid userId)
        {
            string userCacheKey = $"user_{userId}";
            _cache.Remove(userCacheKey);
            _logger.LogDebug("User caches invalidated for: {UserId}", userId);
        }
    }
}