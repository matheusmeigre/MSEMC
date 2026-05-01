using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSEMC.Abstractions;
using MSEMC.Configuration;

namespace MSEMC.Services;

/// <summary>
/// Implementação padrão da governança de destinatários com suporte a Wildcards (*).
/// </summary>
public sealed class RecipientGovernanceService : IRecipientGovernanceService
{
    private readonly GovernanceOptions _options;
    private readonly ILogger<RecipientGovernanceService> _logger;

    public RecipientGovernanceService(
        IOptions<GovernanceOptions> options,
        ILogger<RecipientGovernanceService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsAllowed(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        var normalizedEmail = email.Trim().ToLowerInvariant();

        // 1. Blacklist tem prioridade total (se bater, bloqueia)
        if (_options.RecipientBlacklist.Any(pattern => MatchesPattern(normalizedEmail, pattern)))
        {
            _logger.LogWarning("Envio BLOQUEADO: O destinatário {Email} está na Blacklist.", email);
            return false;
        }

        // 2. Se a Whitelist estiver vazia, todos são permitidos (exceto os da Blacklist)
        if (_options.RecipientWhitelist == null || !_options.RecipientWhitelist.Any())
        {
            return true;
        }

        // 3. Se a Whitelist estiver preenchida, deve bater com algum padrão
        if (_options.RecipientWhitelist.Any(pattern => MatchesPattern(normalizedEmail, pattern)))
        {
            return true;
        }

        _logger.LogWarning("Envio BLOQUEADO: O destinatário {Email} NÃO está na Whitelist.", email);
        return false;
    }

    public IEnumerable<string> FilterAllowed(IEnumerable<string> emails)
    {
        return emails.Where(IsAllowed);
    }

    private static bool MatchesPattern(string email, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;

        var normalizedPattern = pattern.Trim().ToLowerInvariant();

        // Match Exato
        if (normalizedPattern == email) return true;

        // Wildcard no início: *@dominio.com
        if (normalizedPattern.StartsWith("*"))
        {
            var suffix = normalizedPattern.Substring(1);
            return email.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        // Wildcard no fim: admin*
        if (normalizedPattern.EndsWith("*"))
        {
            var prefix = normalizedPattern.Substring(0, normalizedPattern.Length - 1);
            return email.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
