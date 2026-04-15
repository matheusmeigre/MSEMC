using System.ComponentModel.DataAnnotations;

namespace MSEMC.Configuration;

/// <summary>
/// Opções fortemente tipadas para conexão com o RabbitMQ.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required(ErrorMessage = "RabbitMQ Host is required")]
    public string? Host { get; init; }

    public string Username { get; init; } = "guest";

    public string Password { get; init; } = "guest";

    public ushort Port { get; init; } = 5672;
}
