using AutoNext.Plotform.App.Backoffice.Components;
using AutoNext.Plotform.App.Backoffice.Integrations.Core;
using AutoNext.Plotform.App.Backoffice.Models.Common;
using BlazorBootstrap;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register BlazorBootstrap
builder.Services.AddBlazorBootstrap();


var apiGateway = builder.Configuration.GetSection("ApiGateway");

builder.Services.Configure<ApiGateway>(apiGateway);

var gatewayBaseUrl = apiGateway.Get<ApiGateway>()?.BaseUrl ?? throw new InvalidOperationException("ApiGateway:BaseUrl is required");

builder.Services.AddHttpClient<IBrandService, BrandService>(client =>
{
    client.BaseAddress = new Uri(gatewayBaseUrl);
    var apiGatewayConfig = apiGateway.Get<ApiGateway>();
    if (apiGatewayConfig?.TimeoutSeconds > 0)
    {
        client.Timeout = TimeSpan.FromSeconds(apiGatewayConfig.TimeoutSeconds);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();