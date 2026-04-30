using AutoNext.Plotform.App.Backoffice.Models.DTO;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AutoNext.Plotform.App.Backoffice.Integrations.AccessControl
{
    public class AuthStateProvider : AuthenticationStateProvider
    {
        private readonly ProtectedLocalStorage _protectedLocalStorage;
        private readonly IAuthService _authService;
        private readonly ILogger<AuthStateProvider> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private UserSessionDto? _currentUser;

        // Tracks whether the Blazor circuit is ready for JS interop
        private bool _isCircuitReady = false;
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

        // Anonymous state returned during prerender
        private static readonly AuthenticationState _anonymous =
            new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        public AuthStateProvider(
            ProtectedLocalStorage protectedLocalStorage,
            IAuthService authService,
            ILogger<AuthStateProvider> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _protectedLocalStorage = protectedLocalStorage;
            _authService = authService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                // ── PHASE 1: Prerender ──────────────────────────────────────────
                // Circuit isn't ready yet — JS interop is unavailable.
                // Return anonymous immediately so <Authorizing> never gets stuck.
                if (!_isCircuitReady)
                    return _anonymous;

                // ── PHASE 2: Circuit ready ──────────────────────────────────────
                // Now we can safely read ProtectedLocalStorage.
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

        /// <summary>
        /// Call this from a root-level component (e.g. Routes.razor or MainLayout)
        /// inside OnAfterRenderAsync(firstRender: true) to signal the circuit is ready.
        /// </summary>
        public async Task InitializeCircuitAsync()
        {
            if (_isCircuitReady) return;

            _isCircuitReady = true;
            await InitializeAsync();

            // Re-evaluate auth state now that storage is readable
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
                if (result.Success && !string.IsNullOrEmpty(result.Value))
                {
                    _currentUser = JsonConvert.DeserializeObject<UserSessionDto>(result.Value);
                    _logger.LogDebug("User session loaded for: {Email}", _currentUser?.Email);
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop"))
            {
                // Shouldn't reach here now, but kept as a safety net
                _logger.LogWarning("JS interop called too early in LoadUserSessionAsync");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load user session from storage");
            }
        }

        public async Task LoginAsync(AuthResponseDto authResponse)
        {
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

                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null)
                {
                    var claims = BuildClaims(userSession);
                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);
                    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                        new AuthenticationProperties
                        {
                            IsPersistent = true,
                            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                        });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save user session");
            }

            NotifyAuthenticationStateChanged(Task.FromResult(BuildAuthState(userSession)));
        }

        public async Task LogoutAsync()
        {
            if (_currentUser != null)
            {
                try { await _authService.LogoutAsync(_currentUser.UserId, _currentUser.RefreshToken); }
                catch (Exception ex) { _logger.LogError(ex, "Error during logout API call"); }
            }

            _currentUser = null;
            _isInitialized = false;

            try
            {
                await _protectedLocalStorage.DeleteAsync("userSession");

                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null)
                    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete user session");
            }

            NotifyAuthenticationStateChanged(Task.FromResult(_anonymous));
        }

        public async Task<bool> RefreshTokenAsync()
        {
            if (_currentUser == null || string.IsNullOrEmpty(_currentUser.RefreshToken))
                return false;

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
            if (_isCircuitReady && !_isInitialized)
                await InitializeAsync();
            return _currentUser;
        }

        public async Task<string?> GetAccessTokenAsync()
            => (await GetUserSessionAsync())?.AccessToken;

        // ── Helpers ────────────────────────────────────────────────────────────

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