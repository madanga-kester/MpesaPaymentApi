using System.Net;
using System.Text.Json;
using MpesaPaymentApi.Exceptions;

namespace MpesaPaymentApi.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
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
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = ex switch
            {
                MpesaApiException => (int)HttpStatusCode.BadGateway,
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Forbidden,
                _ => (int)HttpStatusCode.InternalServerError
            };

            var problem = new
            {
                type = "about:blank",
                title = _env.IsDevelopment() ? ex.GetType().Name : "An unexpected error occurred.",
                status = context.Response.StatusCode,
                detail = _env.IsDevelopment() ? ex.Message : null,
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));

            // TODO: wire to your alerting channel (Slack/PagerDuty/email) for 5xx specifically:
            if (context.Response.StatusCode >= 500)
            {
                // await _alertService.NotifyAsync($"5xx on {context.Request.Path}: {ex.Message}");
            }
        }
    }
}