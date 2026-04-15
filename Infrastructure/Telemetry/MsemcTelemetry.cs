using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MSEMC.Infrastructure.Telemetry;

/// <summary>
/// Instrumentação OpenTelemetry centralizada para o serviço MSEMC.
/// Define métricas personalizadas e fontes de atividade para rastreamento distribuído.
/// </summary>
public static class MsemcTelemetry
{
    public const string ServiceName = "MSEMC";
    public const string ServiceVersion = "1.0.0";

    // ── Activity Source (Distributed Tracing) ──
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    // ── Meters (Metrics) ──
    private static readonly Meter Meter = new(ServiceName, ServiceVersion);

    /// <summary>Contador de e-mails aceitos pela API.</summary>
    public static readonly Counter<long> EmailsAccepted =
        Meter.CreateCounter<long>("msemc.emails.accepted", "emails",
            "Total number of email requests accepted by the API");

    /// <summary>Contador de e-mails entregues com sucesso.</summary>
    public static readonly Counter<long> EmailsDelivered =
        Meter.CreateCounter<long>("msemc.emails.delivered", "emails",
            "Total number of emails successfully delivered via SMTP");

    /// <summary>Contador de e-mails com falha na entrega.</summary>
    public static readonly Counter<long> EmailsFailed =
        Meter.CreateCounter<long>("msemc.emails.failed", "emails",
            "Total number of emails that failed delivery");

    /// <summary>Histograma da duração do envio de e-mail (milissegundos).</summary>
    public static readonly Histogram<double> EmailSendDuration =
        Meter.CreateHistogram<double>("msemc.emails.send_duration", "ms",
            "Duration of email sending operation in milliseconds");

    /// <summary>Contador de requisições recebidas pela API.</summary>
    public static readonly Counter<long> ApiRequests =
        Meter.CreateCounter<long>("msemc.api.requests", "requests",
            "Total number of API requests received");

    /// <summary>Contador de requisições bloqueadas por rate limiting.</summary>
    public static readonly Counter<long> RateLimitedRequests =
        Meter.CreateCounter<long>("msemc.api.rate_limited", "requests",
            "Total number of rate limited (429) requests");
}
