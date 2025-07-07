using System.Diagnostics;
using Reminder.Services;

namespace Reminder.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILoggingService _loggingService;

        public RequestLoggingMiddleware(RequestDelegate next, ILoggingService loggingService)
        {
            _next = next;
            _loggingService = loggingService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestPath = context.Request.Path;
            var requestMethod = context.Request.Method;
            var userAgent = context.Request.Headers.UserAgent.ToString();
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            using (_loggingService.PushProperty("RequestPath", requestPath))
            using (_loggingService.PushProperty("RequestMethod", requestMethod))
            using (_loggingService.PushProperty("UserAgent", userAgent))
            using (_loggingService.PushProperty("IpAddress", ipAddress))
            {
                try
                {
                    _loggingService.LogInformation("HTTP {RequestMethod} {RequestPath} started from {IpAddress}",
                        requestMethod, requestPath, ipAddress);

                    await _next(context);

                    stopwatch.Stop();
                    var statusCode = context.Response.StatusCode;

                    _loggingService.LogInformation("HTTP {RequestMethod} {RequestPath} completed with status {StatusCode} in {ElapsedMs}ms",
                        requestMethod, requestPath, statusCode, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _loggingService.LogError(ex, "HTTP {RequestMethod} {RequestPath} failed after {ElapsedMs}ms",
                        requestMethod, requestPath, stopwatch.ElapsedMilliseconds);
                    throw;
                }
            }
        }
    }

    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
} 