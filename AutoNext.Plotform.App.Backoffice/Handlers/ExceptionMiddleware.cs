using System.Net;
using System.Text.Json;
using AutoNext.Plotform.App.Backoffice.Models.Common;
using Serilog;

namespace AutoNext.Plotform.App.Backoffice.Handlers
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionMiddleware(RequestDelegate next,
                                   ILogger<ExceptionMiddleware> logger,
                                   IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var traceId = context.TraceIdentifier;

            // ✅ Log with full context
            _logger.LogError(ex,
                "Unhandled exception. TraceId: {TraceId} | Path: {Path} | Method: {Method}",
                traceId,
                context.Request.Path,
                context.Request.Method);

            var statusCode = ex switch
            {
                UnauthorizedAccessException => HttpStatusCode.Unauthorized,
                KeyNotFoundException => HttpStatusCode.NotFound,
                ArgumentException => HttpStatusCode.BadRequest,
                TimeoutException => HttpStatusCode.GatewayTimeout,
                HttpRequestException => HttpStatusCode.BadGateway,
                _ => HttpStatusCode.InternalServerError
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var response = new ErrorResponse
            {
                StatusCode = (int)statusCode,
                Message = GetUserFriendlyMessage(ex),
                TraceId = traceId,
                // ✅ Only expose details in Development
                Details = _env.IsDevelopment() ? ex.ToString() : null
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }

        private static string GetUserFriendlyMessage(Exception ex) => ex switch
        {
            UnauthorizedAccessException => "You are not authorized to perform this action.",
            KeyNotFoundException => "The requested resource was not found.",
            ArgumentException => "Invalid request. Please check your input.",
            TimeoutException => "The request timed out. Please try again.",
            HttpRequestException => "Unable to reach a required service. Please try again later.",
            _ => "An unexpected error occurred. Please try again."
        };
    }
}