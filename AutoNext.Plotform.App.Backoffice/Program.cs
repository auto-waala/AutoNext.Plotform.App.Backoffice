using AutoNext.Plotform.App.Backoffice.Components;
using AutoNext.Plotform.App.Backoffice.Handlers;
using AutoNext.Plotform.App.Backoffice.Integrations.AccessControl;
using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Common;
using AutoNext.Plotform.App.Backoffice.Models.Mapers.Core;
using BlazorBootstrap;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Authorization;
using Radzen;
using Serilog;
using AutoNext.Plotform.App.Backoffice.Integrations.Listings;
using AutoNext.Plotform.App.Backoffice.Integrations.Blob;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http", Serilog.Events.LogEventLevel.Error)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "Logs/app-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

try
{
    Log.Information("Starting AutoNext Backoffice...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // ===== AUTHENTICATION SERVICES - Required for [Authorize] attribute =====
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "JWT";
        options.DefaultChallengeScheme = "JWT";
    })
    .AddJwtBearer("JWT", options =>
    {
        // Disable JWT validation since we handle it manually
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = false,
            ValidateLifetime = false
        };
    });

    builder.Services.AddAuthorization();
    builder.Services.AddCascadingAuthenticationState();

    // Add Razor Components
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // Third-party UI components
    builder.Services.AddBlazorBootstrap();
    builder.Services.AddRadzenComponents();

    // Core services
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<ProtectedLocalStorage>();
    builder.Services.AddScoped<CircuitHandler, BlazorExceptionHandler>();

    // Application services
    builder.Services.AddSingleton<LoaderService>();
    builder.Services.AddSingleton<ToastService>();

    // ===== AUTHENTICATION & AUTHORIZATION SERVICES =====

    // Register AuthStateProvider
    builder.Services.AddScoped<AuthenticationStateProvider, AuthStateProvider>();
    builder.Services.AddScoped<AuthStateProvider>(sp =>
        (AuthStateProvider)sp.GetRequiredService<AuthenticationStateProvider>());

    // Add authorization policies
    builder.Services.AddAuthorizationCore(options =>
    {
        AuthorizationPolicies.AddPolicies(options);
    });

    // Add authorization handlers
    builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

    // ===== API GATEWAY CONFIGURATION =====

    var apiGateway = builder.Configuration.GetSection("ApiGateway");
    builder.Services.Configure<ApiGateway>(apiGateway);

    var gatewayBaseUrl = apiGateway.Get<ApiGateway>()?.BaseUrl
        ?? throw new InvalidOperationException("ApiGateway:BaseUrl is required");
    var apiGatewayConfig = apiGateway.Get<ApiGateway>();

    // Register PollyRetryHandler as a transient service
    builder.Services.AddTransient<PollyRetryHandler>();

    // Register TokenAuthorizationHandler for automatic token refresh
    builder.Services.AddTransient<TokenAuthorizationHandler>();

    // ===== HTTP CLIENT REGISTRATIONS =====

    // Auth Service (No token handler needed for auth endpoints)
    builder.Services.AddHttpClient<IAuthService, AuthService>(client =>
    {
        client.BaseAddress = new Uri(gatewayBaseUrl);
        if (apiGatewayConfig?.TimeoutSeconds > 0)
            client.Timeout = TimeSpan.FromSeconds(apiGatewayConfig.TimeoutSeconds);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("User-Agent", "AutoNext-Backoffice");
    })
    .AddHttpMessageHandler<PollyRetryHandler>()
    .SetHandlerLifetime(TimeSpan.FromMinutes(5));

    // Helper method for registering services with token handler
    void RegisterServiceWithTokenHandler<TInterface, TImplementation>()
        where TInterface : class
        where TImplementation : class, TInterface
    {
        builder.Services.AddHttpClient<TInterface, TImplementation>(client =>
        {
            client.BaseAddress = new Uri(gatewayBaseUrl);
            if (apiGatewayConfig?.TimeoutSeconds > 0)
                client.Timeout = TimeSpan.FromSeconds(apiGatewayConfig.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", "AutoNext-Backoffice");
        })
        .AddHttpMessageHandler<PollyRetryHandler>()
        .AddHttpMessageHandler<TokenAuthorizationHandler>()
        .SetHandlerLifetime(TimeSpan.FromMinutes(5));
    }

    // Register all services with token handler
    RegisterServiceWithTokenHandler<IBrandService, BrandService>();
    RegisterServiceWithTokenHandler<ICategoryService, CategoryService>();
    RegisterServiceWithTokenHandler<IColorService, ColorService>();
    RegisterServiceWithTokenHandler<IDocumentTypeService, DocumentTypeService>();
    RegisterServiceWithTokenHandler<IFeatureService, FeatureService>();
    RegisterServiceWithTokenHandler<IFuelTypeService, FuelTypeService>();
    RegisterServiceWithTokenHandler<IInspectionChecklistService, InspectionChecklistService>();
    RegisterServiceWithTokenHandler<ILocationService, LocationService>();
    RegisterServiceWithTokenHandler<IPaymentMethodService, PaymentMethodService>();
    RegisterServiceWithTokenHandler<IServiceTypeService, ServiceTypeService>();
    RegisterServiceWithTokenHandler<IShippingOptionService, ShippingOptionService>();
    RegisterServiceWithTokenHandler<ITaxRateService, TaxRateService>();
    RegisterServiceWithTokenHandler<ITitleTypeService, TitleTypeService>();
    RegisterServiceWithTokenHandler<ITransmissionService, TransmissionService>();
    RegisterServiceWithTokenHandler<IVehicleConditionService, VehicleConditionService>();
    RegisterServiceWithTokenHandler<IVehicleModelService, VehicleModelService>();
    RegisterServiceWithTokenHandler<IVehicleTypeService, VehicleTypeService>();
    RegisterServiceWithTokenHandler<IVehicleVariantService, VehicleVariantService>();
    RegisterServiceWithTokenHandler<IWarrantyTypeService, WarrantyTypeService>();
    RegisterServiceWithTokenHandler<INewlyArrivedService, NewlyArrivedService>();
    RegisterServiceWithTokenHandler<IFeaturedVehicleService, FeaturedVehicleService>();
    RegisterServiceWithTokenHandler<IBlobService, BlobService>();
    // ===== AUTO MAPPER CONFIGURATION =====

    builder.Services.AddAutoMapper(cfg =>
    {
        cfg.AddProfile<MappingProfile>();
        cfg.AddProfile<BrandProfile>();
        cfg.AddProfile<CategoryProfile>();
        cfg.AddProfile<ColorProfile>();
        cfg.AddProfile<DocumentTypeProfile>();
        cfg.AddProfile<FeatureProfile>();
        cfg.AddProfile<InspectionChecklistProfile>();
        cfg.AddProfile<PaymentMethodProfile>();
        cfg.AddProfile<VehicleModelProfile>();
        cfg.AddProfile<ServiceTypeProfile>();
        cfg.AddProfile<ShippingOptionProfile>();
        cfg.AddProfile<TaxRateProfile>();
        cfg.AddProfile<TitleTypeProfile>();
        cfg.AddProfile<VehicleVariantProfile>();
        cfg.AddProfile<VehicleConditionProfile>();
        cfg.AddProfile<WarrantyTypeProfile>();
    });

    // ===== BUILD APPLICATION =====

    var app = builder.Build();

    // Configure the HTTP request pipeline
    app.UseMiddleware<ExceptionMiddleware>();

    // Add root redirect middleware
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        // Redirect root to login
        if (path == "/" || string.IsNullOrEmpty(path))
        {
            context.Response.Redirect("/login");
            return;
        }

        await next();
    });

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    // IMPORTANT: Add these middleware in the correct order
    app.UseAuthentication();
    app.UseAuthorization();

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAntiforgery();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    Log.Information("Application configured successfully, starting...");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AutoNext Backoffice terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}