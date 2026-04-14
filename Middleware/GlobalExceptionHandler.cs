using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MSEMC.Middleware;

/// <summary>
/// Global exception handler implementing IExceptionHandler (.NET 8).
/// Converts unhandled exceptions into RFC 7807 Problem Details responses.
/// Logs structured error information without exposing internals to clients.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.TraceIdentifier;

        logger.LogError(exception,
            "Unhandled exception caught (CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method})",
            correlationId, httpContext.Request.Path, httpContext.Request.Method);

        var (statusCode, title) = MapExceptionToStatusCode(exception);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = GetSafeDetail(exception, httpContext),
            Instance = httpContext.Request.Path,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static (int StatusCode, string Title) MapExceptionToStatusCode(Exception exception) =>
        exception switch
        {
            ArgumentException or FluentValidation.ValidationException
                => (StatusCodes.Status400BadRequest, "Bad Request"),

            UnauthorizedAccessException
                => (StatusCodes.Status401Unauthorized, "Unauthorized"),

            InvalidOperationException
                => (StatusCodes.Status409Conflict, "Conflict"),

            OperationCanceledException
                => (StatusCodes.Status499ClientClosedRequest, "Client Closed Request"),

            TimeoutException
                => (StatusCodes.Status504GatewayTimeout, "Gateway Timeout"),

            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

    /// <summary>
    /// Returns exception details only in Development; generic message in production.
    /// </summary>
    private static string GetSafeDetail(Exception exception, HttpContext context) =>
        context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment()
            ? exception.ToString()
            : "An unexpected error occurred. Please try again later.";
}
