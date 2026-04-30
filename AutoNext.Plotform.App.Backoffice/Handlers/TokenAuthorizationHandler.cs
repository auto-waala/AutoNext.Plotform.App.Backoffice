using AutoNext.Plotform.App.Backoffice.Integrations.AccessControl;
using System.Net;
using System.Net.Http.Headers;

namespace AutoNext.Plotform.App.Backoffice.Handlers
{
    public class TokenAuthorizationHandler : DelegatingHandler
    {
        private readonly AuthStateProvider _authStateProvider;
        private readonly ILogger<TokenAuthorizationHandler> _logger;
        private static readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        private static bool _isRefreshing = false;

        public TokenAuthorizationHandler(AuthStateProvider authStateProvider, ILogger<TokenAuthorizationHandler> logger)
        {
            _authStateProvider = authStateProvider;
            _logger = logger;
            // REMOVE THIS LINE - DO NOT set InnerHandler here
            // InnerHandler = new HttpClientHandler();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Skip token for auth endpoints to avoid infinite loops
            var isAuthEndpoint = request.RequestUri?.AbsolutePath.Contains("/auth/") == true;

            if (!isAuthEndpoint)
            {
                var accessToken = await _authStateProvider.GetAccessTokenAsync();

                if (!string.IsNullOrEmpty(accessToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                }
            }

            var response = await base.SendAsync(request, cancellationToken);

            // Handle 401 Unauthorized
            if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthEndpoint)
            {
                _logger.LogWarning("Received 401 for {Method} {Url}", request.Method, request.RequestUri);

                var refreshed = await RefreshTokenWithLockAsync();

                if (refreshed)
                {
                    // Retry the original request with new token
                    var newToken = await _authStateProvider.GetAccessTokenAsync();
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
                    response = await base.SendAsync(request, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Token refresh failed, session expired");
                    await _authStateProvider.LogoutAsync();
                }
            }

            return response;
        }

        private async Task<bool> RefreshTokenWithLockAsync()
        {
            await _refreshLock.WaitAsync();
            try
            {
                if (_isRefreshing)
                {
                    // Wait for the ongoing refresh to complete
                    await Task.Delay(100);
                    return true;
                }

                _isRefreshing = true;
                return await _authStateProvider.RefreshTokenAsync();
            }
            finally
            {
                _isRefreshing = false;
                _refreshLock.Release();
            }
        }
    }
}