using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Security.Claims;

namespace AutoNext.Plotform.App.Backoffice.Integrations.AccessControl
{
    public class AuthStateProvider : AuthenticationStateProvider
    {
        private readonly ProtectedLocalStorage _protectedLocalStorage;
        private readonly IAuthService _authService;
        private readonly ILogger<AuthStateProvider> _logger;
        private UserSessionDto? _currentUser;
        private bool _isCircuitReady = false;
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private static readonly AuthenticationState _anonymous = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        public AuthStateProvider(
            ProtectedLocalStorage protectedLocalStorage,
            IAuthService authService,
            ILogger<AuthStateProvider> logger)
        {
            _protectedLocalStorage = protectedLocalStorage;
            _authService = authService;
            _logger = logger;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                _logger.LogDebug("GetAuthenticationStateAsync called. CircuitReady: {CircuitReady}, Initialized: {Initialized}",
                    _isCircuitReady, _isInitialized);

                if (!_isCircuitReady)
                    return _anonymous;

                if (!_isInitialized)
                    await InitializeAsync();

                if (_currentUser == null || !_currentUser.IsAuthenticated)
                    return _anonymous;

                if (_currentUser.IsTokenExpiringSoon)
                    _ = Task.Run(async () => await RefreshTokenAsync());

                return BuildAuthState(_currentUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting authentication state");
                return _anonymous;
            }
        }

        public async Task InitializeCircuitAsync()
        {
            _logger.LogInformation("InitializeCircuitAsync called");
            if (_isCircuitReady) return;

            _isCircuitReady = true;
            await InitializeAsync();
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        private async Task InitializeAsync()
        {
            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized) return;
                await LoadUserSessionAsync();
                _isInitialized = true;
                _logger.LogInformation("AuthStateProvider initialized. HasUser: {HasUser}", _currentUser != null);
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task LoadUserSessionAsync()
        {
            try
            {
                var result = await _protectedLocalStorage.GetAsync<string>("userSession");
                _logger.LogDebug("LoadUserSessionAsync - Storage read completed. Success: {Success}, HasValue: {HasValue}",
                    result.Success, !string.IsNullOrEmpty(result.Value));

                if (result.Success && !string.IsNullOrEmpty(result.Value))
                {
                    _currentUser = JsonConvert.DeserializeObject<UserSessionDto>(result.Value);
                    _logger.LogInformation("User session loaded for: {Email}, Token Length: {TokenLength}",
                        _currentUser?.Email,
                        _currentUser?.AccessToken?.Length ?? 0);
                }
                else
                {
                    _logger.LogDebug("No user session found in storage");
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop"))
            {
                _logger.LogWarning("JS interop called too early in LoadUserSessionAsync");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load user session from storage");
            }
        }

        public async Task LoginAsync(AuthResponseDto authResponse)
        {
            _logger.LogInformation("LoginAsync called for user: {Email}", authResponse.User.Email);

            var userSession = new UserSessionDto
            {
                UserId = authResponse.User.Id,
                Email = authResponse.User.Email,
                FirstName = authResponse.User.FirstName,
                LastName = authResponse.User.LastName,
                UserType = authResponse.User.UserType,
                Roles = authResponse.User.Roles ?? new List<string>(),
                Permissions = authResponse.User.Permissions ?? new List<string>(),
                AccessToken = authResponse.AccessToken,
                RefreshToken = authResponse.RefreshToken,
                TokenExpiry = authResponse.ExpiresAt
            };

            _currentUser = userSession;
            _isInitialized = true;
            _isCircuitReady = true;

            try
            {
                var serialized = JsonConvert.SerializeObject(userSession);
                await _protectedLocalStorage.SetAsync("userSession", serialized);
                _logger.LogInformation("User session saved for: {Email}, Token Length: {TokenLength}",
                    userSession.Email, userSession.AccessToken?.Length ?? 0);

                // Verify the save worked
                var verifyResult = await _protectedLocalStorage.GetAsync<string>("userSession");
                if (verifyResult.Success && !string.IsNullOrEmpty(verifyResult.Value))
                {
                    _logger.LogInformation("Session verification successful");
                }
                else
                {
                    _logger.LogError("Session verification failed - storage write may have issue");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save user session");
            }

            var authState = BuildAuthState(userSession);
            NotifyAuthenticationStateChanged(Task.FromResult(authState));
            _logger.LogInformation("Authentication state changed notification sent");
        }

        public async Task LogoutAsync()
        {
            _logger.LogInformation("LogoutAsync called");

            if (_currentUser != null)
            {
                try
                {
                    await _authService.LogoutAsync(_currentUser.UserId, _currentUser.RefreshToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during logout API call");
                }
            }

            _currentUser = null;
            _isInitialized = false;

            try
            {
                await _protectedLocalStorage.DeleteAsync("userSession");
                _logger.LogInformation("User session deleted from storage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete user session");
            }

            NotifyAuthenticationStateChanged(Task.FromResult(_anonymous));
        }

        public async Task<bool> RefreshTokenAsync()
        {
            _logger.LogInformation("RefreshTokenAsync called");

            if (_currentUser == null || string.IsNullOrEmpty(_currentUser.RefreshToken))
            {
                _logger.LogWarning("Cannot refresh token - no user session or refresh token");
                return false;
            }

            try
            {
                var response = await _authService.RefreshTokenAsync(new RefreshTokenRequestDto
                {
                    RefreshToken = _currentUser.RefreshToken
                });

                if (response != null)
                {
                    _currentUser.AccessToken = response.AccessToken;
                    _currentUser.RefreshToken = response.RefreshToken;
                    _currentUser.TokenExpiry = response.ExpiresAt;

                    var serialized = JsonConvert.SerializeObject(_currentUser);
                    await _protectedLocalStorage.SetAsync("userSession", serialized);

                    _logger.LogInformation("Token refreshed successfully");
                    NotifyAuthenticationStateChanged(Task.FromResult(BuildAuthState(_currentUser)));
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh token");
                await LogoutAsync();
            }

            return false;
        }

        public async Task<UserSessionDto?> GetUserSessionAsync()
        {
            _logger.LogDebug("GetUserSessionAsync called. CircuitReady: {CircuitReady}, Initialized: {Initialized}",
                _isCircuitReady, _isInitialized);

            if (!_isCircuitReady)
            {
                await InitializeCircuitAsync();
            }

            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            return _currentUser;
        }

        public async Task<string?> GetAccessTokenAsync()
        {
            var userSession = await GetUserSessionAsync();
            var token = userSession?.AccessToken;
            _logger.LogDebug("GetAccessTokenAsync returning token of length: {Length}", token?.Length ?? 0);
            return token;
        }

        private AuthenticationState BuildAuthState(UserSessionDto session)
        {
            var identity = new ClaimsIdentity(BuildClaims(session), "Bearer");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        private List<Claim> BuildClaims(UserSessionDto userSession)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userSession.UserId.ToString()),
                new Claim(ClaimTypes.Email, userSession.Email),
                new Claim(ClaimTypes.GivenName, userSession.FirstName),
                new Claim(ClaimTypes.Surname, userSession.LastName),
                new Claim(ClaimTypes.Name, userSession.FullName),
                new Claim("UserType", userSession.UserType),
                new Claim("AccessToken", userSession.AccessToken),
                new Claim("RefreshToken", userSession.RefreshToken),
                new Claim("TokenExpiry", userSession.TokenExpiry.ToString("O"))
            };

            foreach (var role in userSession.Roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            foreach (var permission in userSession.Permissions)
                claims.Add(new Claim("Permission", permission));

            return claims;
        }
    }
}