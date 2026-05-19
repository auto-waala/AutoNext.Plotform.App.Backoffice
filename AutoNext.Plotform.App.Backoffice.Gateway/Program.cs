using Yarp.ReverseProxy.Configuration;
namespace AutoNext.Plotform.App.Backoffice.Gateway;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add YARP reverse proxy with config filter
        builder.Services.AddSingleton<IProxyConfigFilter, YarpEnvironmentVariablesConfigFilter>();

        builder.Services.AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
            .AddConfigFilter<YarpEnvironmentVariablesConfigFilter>();

        // ============ COMPLETE CORS CONFIGURATION ============
        builder.Services.AddCors(options =>
        {
            // Development CORS policy (for local development)
            options.AddPolicy("Development", policy =>
            {
                policy.WithOrigins(
                    // Local Blazor apps
                    "https://localhost:443",
                    "http://localhost:5184",
                    "https://localhost:7184",
                    "http://localhost:5000",
                    "https://localhost:5001",
                    // Local test clients
                    "http://localhost:4200",
                    "http://localhost:3000",
                    // Azure Dev URLs
                    "https://autonext-backoffice-dev.services.azurewebsites.betalen.in"
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
            });

            // Azure Development CORS policy (for deployed dev environment)
            options.AddPolicy("AzureDevelopment", policy =>
            {
                policy.WithOrigins(
                    "https://autonext-backoffice-dev.services.azurewebsites.betalen.in",
                    "https://autonext-gateway-dev.services.azurewebsites.betalen.in"
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
            });

            // Production CORS policy
            options.AddPolicy("Production", policy =>
            {
                policy.WithOrigins(
                    "https://autonext-backoffice-prod.services.azurewebsites.betalen.in",
                    "https://autonext-gateway-prod.services.azurewebsites.betalen.in"
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
            });

            // Default policy that works for all environments
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // Add health checks
        builder.Services.AddHealthChecks();

        // Add HTTP client for health checks
        builder.Services.AddHttpClient();

        var app = builder.Build();

        // ============ ENVIRONMENT-BASED CORS SELECTION ============
        if (app.Environment.IsDevelopment())
        {
            app.UseCors("Development");
        }
        else if (app.Environment.EnvironmentName == "AzureDevelopment")
        {
            app.UseCors("AzureDevelopment");
        }
        else if (app.Environment.IsProduction())
        {
            app.UseCors("Production");
        }
        else
        {
            app.UseCors("AllowAll");
        }

        // ============ ERROR LOGGING MIDDLEWARE ============
        app.Use(async (context, next) =>
        {
            // Add security headers
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("X-Frame-Options", "DENY");
            context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");

            // Log incoming requests
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Request: {Method} {Path} {QueryString}",
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString);

            // Capture response for error logging
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            try
            {
                await next();

                // Log if error occurred
                if (context.Response.StatusCode >= 400)
                {
                    responseBody.Seek(0, SeekOrigin.Begin);
                    var responseText = await new StreamReader(responseBody).ReadToEndAsync();

                    if (context.Response.StatusCode >= 500)
                    {
                        logger.LogError("Gateway Error {StatusCode} for {Path}: {Response}",
                            context.Response.StatusCode, context.Request.Path, responseText);
                    }
                    else
                    {
                        logger.LogWarning("Gateway Warning {StatusCode} for {Path}: {Response}",
                            context.Response.StatusCode, context.Request.Path, responseText);
                    }

                    responseBody.Seek(0, SeekOrigin.Begin);
                    await responseBody.CopyToAsync(originalBodyStream);
                }
                else
                {
                    responseBody.Seek(0, SeekOrigin.Begin);
                    await responseBody.CopyToAsync(originalBodyStream);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception in middleware for {Path}", context.Request.Path);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("An error occurred processing your request.");
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        });

        // ============ HEALTH CHECK ENDPOINTS ============

        // Gateway health check endpoint
        app.MapGet("/gateway/health", () => Results.Ok(new
        {
            Status = "Healthy",
            Environment = app.Environment.EnvironmentName,
            Timestamp = DateTime.UtcNow,
            Services = new
            {
                AccessControl = app.Environment.IsDevelopment() ? "https://localhost:7056" :
                               app.Environment.EnvironmentName == "AzureDevelopment" ?
                               "https://autonext-accesscontrole-dev.services.azurewebsites.betalen.in" :
                               "https://autonext-accesscontrole-prod.services.azurewebsites.betalen.in",
                Core = app.Environment.IsDevelopment() ? "https://localhost:7231" :
                      app.Environment.EnvironmentName == "AzureDevelopment" ?
                      "https://autonext-core-dev.services.azurewebsites.betalen.in" :
                      "https://autonext-core-prod.services.azurewebsites.betalen.in",
                Blob = app.Environment.IsDevelopment() ? "https://localhost:7157" :
                      app.Environment.EnvironmentName == "AzureDevelopment" ?
                      "https://autonext-blob-dev.services.azurewebsites.betalen.in" :
                      "https://autonext-blob.services.azurewebsites.betalen.in",
                Listings = app.Environment.IsDevelopment() ? "https://localhost:7231" :
                          app.Environment.EnvironmentName == "AzureDevelopment" ?
                          "https://autonext-listings-dev.services.azurewebsites.betalen.in" :
                          "https://autonext-listings-prod.services.azurewebsites.betalen.in"
            }
        }));

        // Detailed health check for all downstream services
        app.MapGet("/gateway/health/detailed", async (IHttpClientFactory httpClientFactory) =>
        {
            var httpClient = httpClientFactory.CreateClient();
            var results = new Dictionary<string, object>();

            // Check AccessControl health
            try
            {
                var accessControlUrl = app.Environment.IsDevelopment() ? "https://localhost:7056/health" :
                                       app.Environment.EnvironmentName == "AzureDevelopment" ?
                                       "https://autonext-accesscontrole-dev.services.azurewebsites.betalen.in/health" :
                                       "https://autonext-accesscontrole-prod.services.azurewebsites.betalen.in/health";

                var accessControlResponse = await httpClient.GetAsync(accessControlUrl);
                results["AccessControl"] = new { Status = accessControlResponse.IsSuccessStatusCode ? "Healthy" : "Unhealthy", Url = accessControlUrl, StatusCode = (int)accessControlResponse.StatusCode };
            }
            catch (Exception ex)
            {
                results["AccessControl"] = new { Status = "Unhealthy", Error = ex.Message, Url = ex.Message };
            }

            // Check Core health
            try
            {
                var coreUrl = app.Environment.IsDevelopment() ? "https://localhost:7231/health" :
                             app.Environment.EnvironmentName == "AzureDevelopment" ?
                             "https://autonext-core-dev.services.azurewebsites.betalen.in/health" :
                             "https://autonext-core-prod.services.azurewebsites.betalen.in/health";

                var coreResponse = await httpClient.GetAsync(coreUrl);
                results["Core"] = new { Status = coreResponse.IsSuccessStatusCode ? "Healthy" : "Unhealthy", Url = coreUrl, StatusCode = (int)coreResponse.StatusCode };
            }
            catch (Exception ex)
            {
                results["Core"] = new { Status = "Unhealthy", Error = ex.Message, Url = ex.Message };
            }

            // Check Blob health
            try
            {
                var blobUrl = app.Environment.IsDevelopment() ? "https://localhost:7157/health" :
                             app.Environment.EnvironmentName == "AzureDevelopment" ?
                             "https://autonext-blob-dev.services.azurewebsites.betalen.in/health" :
                             "https://autonext-blob.services.azurewebsites.betalen.in/health";

                var blobResponse = await httpClient.GetAsync(blobUrl);
                results["Blob"] = new { Status = blobResponse.IsSuccessStatusCode ? "Healthy" : "Unhealthy", Url = blobUrl, StatusCode = (int)blobResponse.StatusCode };
            }
            catch (Exception ex)
            {
                results["Blob"] = new { Status = "Unhealthy", Error = ex.Message, Url = ex.Message };
            }

            // Check Listings health
            try
            {
                var listingsUrl = app.Environment.IsDevelopment() ? "https://localhost:7231/health" :
                                 app.Environment.EnvironmentName == "AzureDevelopment" ?
                                 "https://autonext-listings-dev.services.azurewebsites.betalen.in/health" :
                                 "https://autonext-listings-prod.services.azurewebsites.betalen.in/health";

                var listingsResponse = await httpClient.GetAsync(listingsUrl);
                results["Listings"] = new { Status = listingsResponse.IsSuccessStatusCode ? "Healthy" : "Unhealthy", Url = listingsUrl, StatusCode = (int)listingsResponse.StatusCode };
            }
            catch (Exception ex)
            {
                results["Listings"] = new { Status = "Unhealthy", Error = ex.Message, Url = ex.Message };
            }

            return Results.Ok(new
            {
                Gateway = "Healthy",
                Environment = app.Environment.EnvironmentName,
                Timestamp = DateTime.UtcNow,
                Services = results
            });
        });

        // Diagnostic endpoint for testing Featured Vehicle API
        app.MapGet("/gateway/diagnostic/featured-vehicle", async (IHttpClientFactory httpClientFactory) =>
        {
            var httpClient = httpClientFactory.CreateClient();
            var results = new Dictionary<string, object>();

            // Determine the base URL
            var listingsBaseUrl = app.Environment.IsDevelopment()
                ? "https://localhost:7231"
                : app.Environment.EnvironmentName == "AzureDevelopment"
                    ? "https://autonext-listings-dev.services.azurewebsites.betalen.in"
                    : "https://autonext-listings-prod.services.azurewebsites.betalen.in";

            // Test direct connection to listings service
            try
            {
                var testUrl = $"{listingsBaseUrl}/api/v1/FeaturedVehicle/active?limit=5";
                var directResponse = await httpClient.GetAsync(testUrl);
                var content = await directResponse.Content.ReadAsStringAsync();

                results["DirectConnection"] = new
                {
                    Url = testUrl,
                    StatusCode = (int)directResponse.StatusCode,
                    IsSuccess = directResponse.IsSuccessStatusCode,
                    ContentPreview = content?.Length > 500 ? content.Substring(0, 500) : content
                };
            }
            catch (Exception ex)
            {
                results["DirectConnectionError"] = new { Error = ex.Message, StackTrace = ex.StackTrace };
            }

            // Get routing configuration
            var reverseProxyConfig = app.Configuration.GetSection("ReverseProxy:Routes");
            var featuredVehicleRoutes = reverseProxyConfig.GetChildren()
                .Where(x => x.Key.Contains("featured", StringComparison.OrdinalIgnoreCase))
                .Select(x => new { RouteName = x.Key, Path = x.GetValue<string>("Match:Path") });

            results["FeaturedVehicleRoutes"] = featuredVehicleRoutes;
            results["Environment"] = app.Environment.EnvironmentName;
            results["ListingsBaseUrl"] = listingsBaseUrl;

            return Results.Ok(results);
        });

        // Map YARP reverse proxy routes
        app.MapReverseProxy();

        app.Run();
    }
}