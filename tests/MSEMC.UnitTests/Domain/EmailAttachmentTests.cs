using MSEMC.Domain.Entities;

namespace MSEMC.UnitTests.Domain;

/// <summary>
/// Testes para o value object EmailAttachment.
/// Cobre: criação, decodificação Base64 e cálculo aproximado de tamanho.
/// </summary>
public sealed class EmailAttachmentTests
{
    private const string ValidBase64 = "SGVsbG8gV29ybGQ="; // "Hello World"
    private static readonly byte[] ValidBytes = [72, 101, 108, 108, 111, 32, 87, 111, 114, 108, 100];

    // ── Criação ───────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidProperties_ShouldInitializeCorrectly()
    {
        var attachment = new EmailAttachment
        {
            FileName = "relatorio.pdf",
            ContentType = "application/pdf",
            ContentBase64 = ValidBase64
        };

        attachment.FileName.Should().Be("relatorio.pdf");
        attachment.ContentType.Should().Be("application/pdf");
        attachment.ContentBase64.Should().Be(ValidBase64);
    }

    // ── GetContentBytes ───────────────────────────────────────────────────────

    [Fact]
    public void GetContentBytes_WithValidBase64_ShouldReturnDecodedBytes()
    {
        var attachment = new EmailAttachment
        {
            FileName = "test.txt",
            ContentType = "text/plain",
            ContentBase64 = ValidBase64
        };

        var bytes = attachment.GetContentBytes();

        bytes.Should().BeEquivalentTo(ValidBytes);
    }

    [Fact]
    public void GetContentBytes_CalledMultipleTimes_ShouldReturnConsistentResult()
    {
        var attachment = new EmailAttachment
        {
            FileName = "test.txt",
            ContentType = "text/plain",
            ContentBase64 = ValidBase64
        };

        var bytes1 = attachment.GetContentBytes();
        var bytes2 = attachment.GetContentBytes();

        bytes1.Should().BeEquivalentTo(bytes2);
    }

    [Fact]
    public void GetContentBytes_WithInvalidBase64_ShouldThrowFormatException()
    {
        var attachment = new EmailAttachment
        {
            FileName = "test.txt",
            ContentType = "text/plain",
            ContentBase64 = "NOT_VALID_BASE64!!!"
        };

        var act = () => attachment.GetContentBytes();
        act.Should().Throw<FormatException>();
    }

    // ── GetApproximateSizeBytes ───────────────────────────────────────────────

    [Fact]
    public void GetApproximateSizeBytes_ShouldReturnReasonableApproximation()
    {
        // "Hello World" = 11 bytes → Base64 = 16 chars → approx = 16 * 0.75 = 12
        var attachment = new EmailAttachment
        {
            FileName = "test.txt",
            ContentType = "text/plain",
            ContentBase64 = ValidBase64
        };

        var size = attachment.GetApproximateSizeBytes();

        // Deve estar entre 9 e 16 bytes (margem para padding Base64)
        size.Should().BeInRange(9, 16);
    }

    // ── Imutabilidade (record) ────────────────────────────────────────────────

    [Fact]
    public void EmailAttachment_IsRecord_ShouldSupportValueEquality()
    {
        var a1 = new EmailAttachment { FileName = "f.pdf", ContentType = "application/pdf", ContentBase64 = ValidBase64 };
        var a2 = new EmailAttachment { FileName = "f.pdf", ContentType = "application/pdf", ContentBase64 = ValidBase64 };

        a1.Should().Be(a2);
    }
}
