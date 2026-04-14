using System.ComponentModel.DataAnnotations;

namespace MSEMC.Configuration;

/// <summary>
/// Strongly-typed options for RabbitMQ connection.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required(ErrorMessage = "RabbitMQ Host is required")]
    public required string Host { get; init; }

    public string Username { get; init; } = "guest";

    public string Password { get; init; } = "guest";

    public ushort Port { get; init; } = 5672;
}
