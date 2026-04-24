using MSEMC.Infrastructure.Templates;
using MSEMC.Domain.Results;

namespace MSEMC.UnitTests.Infrastructure;

/// <summary>
/// Testes unitários para o ScribanTemplateEngine.
/// Valida: renderização de variáveis simples, listas, objetos aninhados,
/// extração de variáveis do AST e tratamento de erros de sintaxe.
/// </summary>
public sealed class ScribanTemplateEngineTests
{
    private readonly ScribanTemplateEngine _engine;

    public ScribanTemplateEngineTests()
    {
        var logger = Substitute.For<ILogger<ScribanTemplateEngine>>();
        _engine = new ScribanTemplateEngine(logger);
    }

    // ── Renderização: Variáveis Simples ───────────────────────────────────────

    [Fact]
    public async Task RenderAsync_WithSimpleVariable_ShouldInterpolate()
    {
        var template = "Olá, {{ nomeUsuario }}!";
        var data = new Dictionary<string, object?> { ["nomeUsuario"] = "João" };

        var result = await _engine.RenderAsync(template, data);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Olá, João!");
    }

    [Fact]
    public async Task RenderAsync_WithMultipleVariables_ShouldInterpolateAll()
    {
        var template = "{{ titulo }} — {{ subtitulo }}";
        var data = new Dictionary<string, object?>
        {
            ["titulo"] = "Bem-vindo",
            ["subtitulo"] = "Sua conta está pronta"
        };

        var result = await _engine.RenderAsync(template, data);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Bem-vindo — Sua conta está pronta");
    }

    [Fact]
    public async Task RenderAsync_WithNumericVariable_ShouldConvertToString()
    {
        var template = "Código: {{ codigoSeguranca }}. Válido por {{ validadeMinutos }} minutos.";
        var data = new Dictionary<string, object?>
        {
            ["codigoSeguranca"] = "482916",
            ["validadeMinutos"] = 10L
        };

        var result = await _engine.RenderAsync(template, data);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("482916");
        result.Value.Should().Contain("10");
    }

    [Fact]
    public async Task RenderAsync_WithBooleanInConditional_ShouldRender()
    {
        var template = "{{ if ativo }}Conta ativa{{ else }}Conta inativa{{ end }}";
        var data = new Dictionary<string, object?> { ["ativo"] = true };

        var result = await _engine.RenderAsync(template, data);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Conta ativa");
    }

    // ── Renderização: Objetos Aninhados ───────────────────────────────────────

    [Fact]
    public async Task RenderAsync_WithNestedObject_ShouldAccessProperties()
    {
        var template = "{{ usuario.nome }} ({{ usuario.email }})";
        var data = new Dictionary<string, object?>
        {
            ["usuario"] = new Dictionary<string, object?>
            {
                ["nome"] = "Maria",
                ["email"] = "maria@email.com"
            }
        };

        var result = await _engine.RenderAsync(template, data);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Maria (maria@email.com)");
    }

    // ── Renderização: Listas ──────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_WithList_ShouldIterateCorrectly()
    {
        var template = "{{ for item in itens }}{{ item.nome }};{{ end }}";
        var data = new Dictionary<string, object?>
        {
            ["itens"] = new List<object?>
            {
                new Dictionary<string, object?> { ["nome"] = "Produto A" },
                new Dictionary<string, object?> { ["nome"] = "Produto B" },
                new Dictionary<string, object?> { ["nome"] = "Produto C" }
            }
        };

        var result = await _engine.RenderAsync(template, data);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Produto A;Produto B;Produto C;");
    }

    [Fact]
    public async Task RenderAsync_WithEmptyList_ShouldRenderEmptyContent()
    {
        var template = "{{ for item in itens }}{{ item.nome }}{{ end }}";
        var data = new Dictionary<string, object?> { ["itens"] = new List<object?>() };

        var result = await _engine.RenderAsync(template, data);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── Renderização: Null / Ausente ──────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_WithNullVariable_ShouldRenderEmpty()
    {
        var template = "Olá, {{ nomeUsuario }}!";
        var data = new Dictionary<string, object?> { ["nomeUsuario"] = null };

        var result = await _engine.RenderAsync(template, data);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Olá, !");
    }

    [Fact]
    public async Task RenderAsync_WithMissingVariable_ShouldRenderEmpty()
    {
        // Scriban com StrictVariables=false silencia variáveis ausentes (renderiza vazio)
        var template = "Valor: {{ variavelInexistente }}";
        var data = new Dictionary<string, object?>();

        var result = await _engine.RenderAsync(template, data);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Valor: ");
    }

    // ── Renderização: Condicional ─────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_WithConditionalBlock_WhenFalse_ShouldSkipBlock()
    {
        var template = "{{ if linkPagamento }}Pagar agora{{ end }}";
        var data = new Dictionary<string, object?> { ["linkPagamento"] = null };

        var result = await _engine.RenderAsync(template, data);

        result.IsSuccess.Should().BeTrue();
        result.Value.Trim().Should().BeEmpty();
    }

    // ── Erro de Sintaxe ───────────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_WithSyntaxError_ShouldReturnFailResult()
    {
        var template = "{{ for item in }}broken{{ end }}"; // sintaxe inválida
        var data = new Dictionary<string, object?>();

        var result = await _engine.RenderAsync(template, data);

        result.IsSuccess.Should().BeFalse();
        (result.Error!.Contains("syntax error") || result.Error.Contains("Template syntax error")).Should().BeTrue(
            because: $"Expected error to mention syntax error, but got: {result.Error}");
    }

    // ── Extração de Variáveis ─────────────────────────────────────────────────

    [Fact]
    public void ExtractVariables_WithSimpleTemplate_ShouldReturnAllVariableNames()
    {
        var template = "Olá, {{ nomeUsuario }}! Código: {{ codigoSeguranca }}.";

        var variables = _engine.ExtractVariables(template);

        variables.Should().Contain("nomeUsuario").And.Contain("codigoSeguranca");
    }

    [Fact]
    public void ExtractVariables_WithLoopVariable_ShouldReturnIterableVariable()
    {
        var template = "{{ for item in itens }}{{ item.nome }}{{ end }}";

        var variables = _engine.ExtractVariables(template);

        variables.Should().Contain("itens");
    }

    [Fact]
    public void ExtractVariables_WithConditional_ShouldReturnConditionVariable()
    {
        var template = "{{ if linkPagamento }}clique{{ end }}";

        var variables = _engine.ExtractVariables(template);

        variables.Should().Contain("linkPagamento");
    }

    [Fact]
    public void ExtractVariables_WithNoVariables_ShouldReturnEmptySet()
    {
        var template = "<html><body><p>Conteúdo fixo sem variáveis.</p></body></html>";

        var variables = _engine.ExtractVariables(template);

        variables.Should().BeEmpty();
    }

    [Fact]
    public void ExtractVariables_IsCaseInsensitive_ShouldNormalize()
    {
        var template = "{{ NomeUsuario }} e {{ nomeUsuario }}";

        var variables = _engine.ExtractVariables(template);

        // Deve ter apenas uma entrada (case-insensitive)
        variables.Should().Contain(v => v.Equals("NomeUsuario", StringComparison.OrdinalIgnoreCase));
    }

    // ── Template Vazio ────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_WithEmptyTemplate_ShouldReturnEmpty()
    {
        var result = await _engine.RenderAsync(string.Empty, new Dictionary<string, object?>());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── Cancelamento ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        var template = "{{ nomeUsuario }}";
        var data = new Dictionary<string, object?> { ["nomeUsuario"] = "test" };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _engine.RenderAsync(template, data, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
