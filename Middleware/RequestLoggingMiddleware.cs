using System.Diagnostics;

namespace MSEMC.Middleware;

/// <summary>
/// Middleware que enriquece cada requisição com um correlation ID e registra
/// o ciclo de vida da requisição/resposta com informações de tempo.
/// Adiciona o header X-Correlation-Id nas respostas para rastreamento distribuído.
/// </summary>
public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path.ToString(),
            ["RequestMethod"] = context.Request.Method
        }))
        {
            var stopwatch = Stopwatch.StartNew();

            logger.LogInformation(
                "→ {Method} {Path} iniciado (CorrelationId: {CorrelationId})",
                context.Request.Method, context.Request.Path, correlationId);

            try
            {
                await next(context);
            }
            finally
            {
                stopwatch.Stop();

                logger.LogInformation(
                    "← {Method} {Path} concluído com {StatusCode} em {ElapsedMs}ms (CorrelationId: {CorrelationId})",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
            }
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existing)
            && !string.IsNullOrWhiteSpace(existing))
        {
            return existing.ToString();
        }

        return Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
    }
}
