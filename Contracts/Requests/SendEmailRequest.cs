using System.Text.Json;
using MSEMC.Domain.Entities;

namespace MSEMC.Contracts.Requests;

/// <summary>
/// Contrato de requisição para envio de e-mail. Suporta dois modos mutuamente exclusivos:
/// 
/// MODO TEMPLATE (recomendado): Fornece templateId + data. O backend renderiza o HTML.
/// MODO RAW (legado): Fornece subject + body diretamente. Sem processamento de template.
/// 
/// Os modos são validados pelo SendEmailRequestValidator — ambos não podem coexistir.
/// </summary>
public sealed record SendEmailRequest(
    // ─── Destinatário ────────────────────────────────────────────────────────────
    string Recipient,
    List<string>? CcRecipients = null,
    List<string>? BccRecipients = null,

    // ─── Modo Template (novo) ────────────────────────────────────────────────────
    /// <summary>Ex: "autenticacao/codigo-seguranca". Mutuamente exclusivo com Body.</summary>
    string? TemplateId = null,
    /// <summary>Locale para seleção do template. Ex: "pt-BR". Null usa fallback "default".</summary>
    string? Locale = null,
    /// <summary>Dados dinâmicos em camelCase para preencher as variáveis do template.</summary>
    JsonElement? Data = default,

    // ─── Modo Raw / Legado ───────────────────────────────────────────────────────
    /// <summary>Assunto do e-mail. Se TemplateId fornecido, sobrescreve o subjectTemplate do metadata.</summary>
    string? Subject = null,
    /// <summary>Corpo HTML/texto do e-mail. Mutuamente exclusivo com TemplateId.</summary>
    string? Body = null,
    bool IsHtml = true,

    // ─── Attachments (novo) ──────────────────────────────────────────────────────
    /// <summary>Arquivos pré-gerados para anexar ao e-mail (Base64). Máx configured via TemplateOptions.</summary>
    List<EmailAttachment>? Attachments = null
);
