namespace AutoNext.Plotform.App.Backoffice.Handlers
{
    public class AuthenticationCheckMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthenticationCheckMiddleware> _logger;
        private readonly List<string> _publicPaths = new()
        {
            "/login",
            "/forgot-password",
            "/reset-password",
            "/unauthorized",
            "/register",
            "/_framework",
            "/css",
            "/js",
            "/lib",
            "/_content",
            "/_blazor"
        };

        public AuthenticationCheckMiddleware(RequestDelegate next, ILogger<AuthenticationCheckMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";

            // Skip middleware for public paths and Blazor internals
            if (IsPublicPath(path) || IsBlazorInternalPath(path))
            {
                await _next(context);
                return;
            }

            // Check if user is authenticated via cookies/session
            var isAuthenticated = context.User?.Identity?.IsAuthenticated == true;

            if (!isAuthenticated && !IsStaticFile(path))
            {
                _logger.LogWarning("Unauthenticated access attempt to {Path}", path);

                // For AJAX/API requests, return 401
                if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                    context.Request.Headers["Accept"].ToString().Contains("application/json"))
                {
                    context.Response.StatusCode = 401;
                    return;
                }

                // For regular page requests, redirect to login
                var returnUrl = Uri.EscapeDataString(path);
                context.Response.Redirect($"/login?returnUrl={returnUrl}");
                return;
            }

            await _next(context);
        }

        private bool IsPublicPath(string path)
        {
            return _publicPaths.Any(p => path.StartsWith(p));
        }

        private bool IsBlazorInternalPath(string path)
        {
            return path.Contains("/_blazor") ||
                   path.Contains("blazor") ||
                   path.Contains("signalr") ||
                   path.Contains("negotiate");
        }

        private bool IsStaticFile(string path)
        {
            var staticExtensions = new[] {
                ".css", ".js", ".png", ".jpg", ".jpeg", ".gif", ".ico",
                ".svg", ".woff", ".woff2", ".ttf", ".eot", ".map",
                ".json", ".webmanifest"
            };
            return staticExtensions.Any(ext => path.EndsWith(ext));
        }
    }
}