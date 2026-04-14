using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MSEMC.Infrastructure.Telemetry;

/// <summary>
/// Centralized OpenTelemetry instrumentation for the MSEMC service.
/// Defines custom metrics and activity sources for distributed tracing.
/// </summary>
public static class MsemcTelemetry
{
    public const string ServiceName = "MSEMC";
    public const string ServiceVersion = "1.0.0";

    // ── Activity Source (Distributed Tracing) ──
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    // ── Meters (Metrics) ──
    private static readonly Meter Meter = new(ServiceName, ServiceVersion);

    /// <summary>Counter for total emails accepted by the API.</summary>
    public static readonly Counter<long> EmailsAccepted =
        Meter.CreateCounter<long>("msemc.emails.accepted", "emails",
            "Total number of email requests accepted by the API");

    /// <summary>Counter for total emails successfully delivered.</summary>
    public static readonly Counter<long> EmailsDelivered =
        Meter.CreateCounter<long>("msemc.emails.delivered", "emails",
            "Total number of emails successfully delivered via SMTP");

    /// <summary>Counter for total emails that failed delivery.</summary>
    public static readonly Counter<long> EmailsFailed =
        Meter.CreateCounter<long>("msemc.emails.failed", "emails",
            "Total number of emails that failed delivery");

    /// <summary>Histogram for email sending duration (milliseconds).</summary>
    public static readonly Histogram<double> EmailSendDuration =
        Meter.CreateHistogram<double>("msemc.emails.send_duration", "ms",
            "Duration of email sending operation in milliseconds");

    /// <summary>Counter for total API requests received.</summary>
    public static readonly Counter<long> ApiRequests =
        Meter.CreateCounter<long>("msemc.api.requests", "requests",
            "Total number of API requests received");

    /// <summary>Counter for rate limited requests.</summary>
    public static readonly Counter<long> RateLimitedRequests =
        Meter.CreateCounter<long>("msemc.api.rate_limited", "requests",
            "Total number of rate limited (429) requests");
}
