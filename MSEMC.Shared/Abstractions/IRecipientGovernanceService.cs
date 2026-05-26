using System.Collections.Generic;

namespace MSEMC.Abstractions;

/// <summary>
/// Define as regras de governança para destinatários de e-mail.
/// </summary>
public interface IRecipientGovernanceService
{
    /// <summary>
    /// Verifica se um e-mail específico é permitido pelas regras de Whitelist/Blacklist.
    /// </summary>
    bool IsAllowed(string email);

    /// <summary>
    /// Filtra uma lista de e-mails, retornando apenas os que são permitidos.
    /// </summary>
    IEnumerable<string> FilterAllowed(IEnumerable<string> emails);
}
