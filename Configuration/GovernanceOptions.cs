using System.Collections.Generic;

namespace MSEMC.Configuration;

/// <summary>
/// Opções de governança para controle de destinatários.
/// </summary>
public sealed class GovernanceOptions
{
    public const string SectionName = "Governance";

    /// <summary>
    /// Lista de e-mails ou padrões (ex: *@empresa.com) permitidos.
    /// Se estiver vazia, a whitelist é ignorada (modo aberto).
    /// </summary>
    public List<string> RecipientWhitelist { get; set; } = new();

    /// <summary>
    /// Lista de e-mails ou padrões (ex: *@spam.com) terminantemente bloqueados.
    /// </summary>
    public List<string> RecipientBlacklist { get; set; } = new();
}
