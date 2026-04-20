using AutoNext.Plotform.App.Backoffice.Components;
using AutoNext.Plotform.App.Backoffice.Handlers;
using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Common;
using BlazorBootstrap;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Radzen;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
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

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddBlazorBootstrap();

    builder.Services.AddRadzenComponents();

    builder.Services.AddSingleton<LoaderService>();
    builder.Services.AddSingleton<ToastService>();

    var apiGateway = builder.Configuration.GetSection("ApiGateway");
    builder.Services.Configure<ApiGateway>(apiGateway);

    var gatewayBaseUrl = apiGateway.Get<ApiGateway>()?.BaseUrl
        ?? throw new InvalidOperationException("ApiGateway:BaseUrl is required");

    builder.Services.AddHttpClient<IBrandService, BrandService>(client =>
    {
        client.BaseAddress = new Uri(gatewayBaseUrl);
        var apiGatewayConfig = apiGateway.Get<ApiGateway>();
        if (apiGatewayConfig?.TimeoutSeconds > 0)
            client.Timeout = TimeSpan.FromSeconds(apiGatewayConfig.TimeoutSeconds);
    });

    builder.Services.AddScoped<CircuitHandler, BlazorExceptionHandler>();

    var app = builder.Build();

    app.UseMiddleware<ExceptionMiddleware>();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAntiforgery();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

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