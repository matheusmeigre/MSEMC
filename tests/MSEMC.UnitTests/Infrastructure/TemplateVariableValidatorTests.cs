using MSEMC.Domain.Entities;
using MSEMC.Infrastructure.Templates;

namespace MSEMC.UnitTests.Infrastructure;

/// <summary>
/// Testes unitários para TemplateVariableValidator.
/// Cobre: todos os campos presentes, campos faltantes, opcionais ausentes (ok), template sem variáveis.
/// </summary>
public sealed class TemplateVariableValidatorTests
{
    private readonly TemplateVariableValidator _validator = new();

    // ── Happy Path ────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_AllRequiredVariablesPresent_ShouldReturnNull()
    {
        var metadata = new TemplateMetadata
        {
            Name = "OTP",
            Description = "desc",
            Domain = "auth",
            RequiredVariables = ["nomeUsuario", "codigoSeguranca", "validadeMinutos"]
        };

        var data = new Dictionary<string, object?>
        {
            ["nomeUsuario"] = "João",
            ["codigoSeguranca"] = "123456",
            ["validadeMinutos"] = 10
        };

        var error = _validator.Validate(metadata, data);

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_RequiredAndOptionalPresent_ShouldReturnNull()
    {
        var metadata = new TemplateMetadata
        {
            Name = "OTP", Description = "desc", Domain = "auth",
            RequiredVariables = ["nomeUsuario"],
            OptionalVariables = ["nomeEmpresa"]
        };

        var data = new Dictionary<string, object?>
        {
            ["nomeUsuario"] = "João",
            ["nomeEmpresa"] = "Acme"
        };

        var error = _validator.Validate(metadata, data);

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_OptionalVariableAbsent_ShouldReturnNull()
    {
        // Variáveis opcionais ausentes NÃO devem bloquear a renderização
        var metadata = new TemplateMetadata
        {
            Name = "Boas-vindas", Description = "desc", Domain = "onboarding",
            RequiredVariables = ["nomeUsuario"],
            OptionalVariables = ["nomeEmpresa", "linkPlataforma"]
        };

        var data = new Dictionary<string, object?> { ["nomeUsuario"] = "Maria" };

        var error = _validator.Validate(metadata, data);

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_TemplateWithNoRequiredVariables_ShouldAlwaysReturnNull()
    {
        var metadata = new TemplateMetadata
        {
            Name = "Simples", Description = "desc", Domain = "engajamento",
            RequiredVariables = [],
            OptionalVariables = ["titulo"]
        };

        var error = _validator.Validate(metadata, new Dictionary<string, object?>());

        error.Should().BeNull();
    }

    // ── Fail Fast ─────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_OneRequiredVariableMissing_ShouldReturnErrorWithVariableName()
    {
        var metadata = new TemplateMetadata
        {
            Name = "OTP", Description = "desc", Domain = "auth",
            RequiredVariables = ["nomeUsuario", "codigoSeguranca", "validadeMinutos"]
        };

        var data = new Dictionary<string, object?>
        {
            ["nomeUsuario"] = "João",
            // codigoSeguranca está faltando
            ["validadeMinutos"] = 10
        };

        var error = _validator.Validate(metadata, data);

        error.Should().NotBeNull();
        error.Should().Contain("codigoSeguranca");
    }

    [Fact]
    public void Validate_MultipleRequiredVariablesMissing_ShouldListAllMissing()
    {
        var metadata = new TemplateMetadata
        {
            Name = "Pedido", Description = "desc", Domain = "ecommerce",
            RequiredVariables = ["nomeCliente", "numeroPedido", "itens", "valorTotal", "enderecoEntrega"]
        };

        var data = new Dictionary<string, object?> { ["nomeCliente"] = "João" };

        var error = _validator.Validate(metadata, data);

        error.Should().NotBeNull();
        error.Should().Contain("numeroPedido")
            .And.Contain("itens")
            .And.Contain("valorTotal")
            .And.Contain("enderecoEntrega");
    }

    [Fact]
    public void Validate_RequiredVariablePresentButNull_ShouldReturnError()
    {
        // Variável presente mas com valor null é equivalente a ausente para Fail-Fast
        var metadata = new TemplateMetadata
        {
            Name = "OTP", Description = "desc", Domain = "auth",
            RequiredVariables = ["codigoSeguranca"]
        };

        var data = new Dictionary<string, object?> { ["codigoSeguranca"] = null };

        var error = _validator.Validate(metadata, data);

        error.Should().NotBeNull();
        error.Should().Contain("codigoSeguranca");
    }

    [Fact]
    public void Validate_EmptyDataDictionary_AllRequired_ShouldListAll()
    {
        var metadata = new TemplateMetadata
        {
            Name = "OTP", Description = "desc", Domain = "auth",
            RequiredVariables = ["a", "b", "c"]
        };

        var error = _validator.Validate(metadata, new Dictionary<string, object?>());

        error.Should().NotBeNull();
        error.Should().Contain("'a'").And.Contain("'b'").And.Contain("'c'");
    }
}
