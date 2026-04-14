using FluentAssertions;
using MSEMC.Domain.Results;

namespace MSEMC.UnitTests.Domain;

public sealed class ResultTests
{
    [Fact]
    public void Ok_ShouldCreateSuccessResult()
    {
        // Act
        var result = Result<string>.Ok("value");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("value");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Fail_ShouldCreateFailureResult()
    {
        // Act
        var result = Result<string>.Fail("error message");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("error message");
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Match_OnSuccess_ShouldExecuteSuccessFunc()
    {
        // Arrange
        var result = Result<int>.Ok(42);

        // Act
        var output = result.Match(
            onSuccess: v => $"Value: {v}",
            onFailure: e => $"Error: {e}");

        // Assert
        output.Should().Be("Value: 42");
    }

    [Fact]
    public void Match_OnFailure_ShouldExecuteFailureFunc()
    {
        // Arrange
        var result = Result<int>.Fail("not found");

        // Act
        var output = result.Match(
            onSuccess: v => $"Value: {v}",
            onFailure: e => $"Error: {e}");

        // Assert
        output.Should().Be("Error: not found");
    }
}
