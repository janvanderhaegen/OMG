namespace OMG.Management.Domain.Common;

public sealed record Error(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

public class Result
{
    public bool IsSuccess { get; }
    public Error? Error { get; }

    public bool IsFailure => !IsSuccess;

    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);

    public static Result Failure(string code, string message, IReadOnlyDictionary<string, string[]>? validationErrors = null) =>
        new(false, new Error(code, message, validationErrors));
}

public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, Error? error)
        : base(isSuccess, error)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null);

    public static new Result<T> Failure(string code, string message, IReadOnlyDictionary<string, string[]>? validationErrors = null) =>
        new(false, default, new Error(code, message, validationErrors));
}

