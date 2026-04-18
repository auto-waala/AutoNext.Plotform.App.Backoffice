var builder = WebApplication.CreateBuilder(args);

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

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

// Optional: Add custom headers for security
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    await next();
});

// Map YARP reverse proxy routes
app.MapReverseProxy();

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
              "https://autonext-core-prod.services.azurewebsites.betalen.in"
    }
}));

// Detailed health check for all downstream services
app.MapGet("/gateway/health/detailed", async () =>
{
    var httpClient = new HttpClient();
    var results = new Dictionary<string, object>();

    // Check AccessControl health
    try
    {
        var accessControlUrl = app.Environment.IsDevelopment() ? "https://localhost:7056/health" :
                               app.Environment.EnvironmentName == "AzureDevelopment" ?
                               "https://autonext-accesscontrole-dev.services.azurewebsites.betalen.in/health" :
                               "https://autonext-accesscontrole-prod.services.azurewebsites.betalen.in/health";

        var accessControlResponse = await httpClient.GetAsync(accessControlUrl);
        results["AccessControl"] = new { Status = accessControlResponse.IsSuccessStatusCode ? "Healthy" : "Unhealthy", Url = accessControlUrl };
    }
    catch (Exception ex)
    {
        results["AccessControl"] = new { Status = "Unhealthy", Error = ex.Message };
    }

    // Check Core health
    try
    {
        var coreUrl = app.Environment.IsDevelopment() ? "https://localhost:7231/health" :
                     app.Environment.EnvironmentName == "AzureDevelopment" ?
                     "https://autonext-core-dev.services.azurewebsites.betalen.in/health" :
                     "https://autonext-core-prod.services.azurewebsites.betalen.in/health";

        var coreResponse = await httpClient.GetAsync(coreUrl);
        results["Core"] = new { Status = coreResponse.IsSuccessStatusCode ? "Healthy" : "Unhealthy", Url = coreUrl };
    }
    catch (Exception ex)
    {
        results["Core"] = new { Status = "Unhealthy", Error = ex.Message };
    }

    return Results.Ok(new
    {
        Gateway = "Healthy",
        Environment = app.Environment.EnvironmentName,
        Timestamp = DateTime.UtcNow,
        Services = results
    });
});

app.Run();