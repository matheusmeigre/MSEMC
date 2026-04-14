using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Retry;
using Polly.Timeout;

namespace MSEMC.Infrastructure.Resilience;

/// <summary>
/// Configures resilience pipelines for the MSEMC service.
/// Uses Polly v8 Resilience Pipelines with retry, circuit breaker, and timeout strategies.
/// </summary>
public static class ResiliencePolicies
{
    public const string SmtpPipelineName = "smtp-resilience";

    /// <summary>
    /// Registers the SMTP resilience pipeline in the DI container.
    /// Pipeline order: Timeout (outer) → Retry → Circuit Breaker → Timeout (inner per attempt).
    /// </summary>
    public static IServiceCollection AddSmtpResilience(this IServiceCollection services)
    {
        services.AddResiliencePipeline(SmtpPipelineName, builder =>
        {
            // Total timeout: 30s for the entire operation including retries
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(30),
                Name = "TotalTimeout"
            });

            // Retry: 3 attempts with exponential backoff (2s, 4s, 8s)
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex =>
                    ex is not (OperationCanceledException or ArgumentException)),
                OnRetry = static args =>
                {
                    // Logging is available via the ResilienceContext
                    return ValueTask.CompletedTask;
                },
                Name = "SmtpRetry"
            });

            // Circuit Breaker: opens after 50% failure rate in 30s window
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(15),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex =>
                    ex is not (OperationCanceledException or ArgumentException)),
                Name = "SmtpCircuitBreaker"
            });

            // Per-attempt timeout: 10s for each individual send attempt
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(10),
                Name = "PerAttemptTimeout"
            });
        });

        return services;
    }
}
