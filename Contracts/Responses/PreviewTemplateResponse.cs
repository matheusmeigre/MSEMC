namespace MSEMC.Contracts.Responses;

/// <summary>
/// Resposta do endpoint de preview de template.
/// Contém o HTML renderizado pronto para exibição em um iframe do frontend.
/// </summary>
public sealed record PreviewTemplateResponse(
    string TemplateId,
    string Locale,
    string Subject,
    string RenderedHtml,
    DateTimeOffset RenderedAt
);
