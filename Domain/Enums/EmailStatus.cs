namespace MSEMC.Domain.Enums;

/// <summary>
/// Representa o status do ciclo de vida de uma mensagem de e-mail.
/// </summary>
public enum EmailStatus
{
    /// <summary>Mensagem criada mas ainda não enfileirada para entrega.</summary>
    Pending = 0,

    /// <summary>Mensagem aceita e enfileirada para processamento assíncrono.</summary>
    Queued = 1,

    /// <summary>Mensagem sendo enviada via SMTP no momento.</summary>
    Sending = 2,

    /// <summary>Mensagem entregue com sucesso ao servidor SMTP.</summary>
    Sent = 3,

    /// <summary>Entrega da mensagem falhou após todas as tentativas de retry.</summary>
    Failed = 4,

    /// <summary>Entrega da mensagem falhou e está sendo reprocessada.</summary>
    Retrying = 5
}
