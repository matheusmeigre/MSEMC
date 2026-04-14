namespace MSEMC.Domain.Results;

/// <summary>
/// Represents the outcome of an operation that can either succeed with a value
/// or fail with an error message. Used instead of exceptions for business logic flow.
/// </summary>
public readonly record struct Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public bool IsSuccess { get; }

    private Result(T value)
    {
        Value = value;
        IsSuccess = true;
        Error = null;
    }

    private Result(string error)
    {
        Error = error;
        IsSuccess = false;
        Value = default;
    }

    /// <summary>Creates a successful result containing the specified value.</summary>
    public static Result<T> Ok(T value) => new(value);

    /// <summary>Creates a failed result with the specified error message.</summary>
    public static Result<T> Fail(string error) => new(error);

    /// <summary>
    /// Matches the result to one of two functions depending on success or failure.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}
