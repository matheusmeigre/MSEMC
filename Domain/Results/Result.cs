namespace MSEMC.Domain.Results;

/// <summary>
/// Representa o resultado de uma operação que pode ter sucesso com um valor
/// ou falhar com uma mensagem de erro. Usado no lugar de exceções para o fluxo de lógica de negócio.
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

    /// <summary>Cria um resultado de sucesso contendo o valor especificado.</summary>
    public static Result<T> Ok(T value) => new(value);

    /// <summary>Cria um resultado de falha com a mensagem de erro especificada.</summary>
    public static Result<T> Fail(string error) => new(error);

    /// <summary>
    /// Executa uma das duas funções dependendo do sucesso ou falha do resultado.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}
