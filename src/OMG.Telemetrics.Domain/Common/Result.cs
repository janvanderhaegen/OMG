namespace OMG.Telemetrics.Domain.Common;

public sealed class Error
{
    public string Code { get; }

    public string Message { get; }

    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    public Error(string code, string message, IReadOnlyDictionary<string, string[]>? validationErrors = null)
    {
        Code = code;
        Message = message;
        ValidationErrors = validationErrors;
    }
}

public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(string code, string message, IDictionary<string, string[]>? validationErrors = null) =>
        new(false, new Error(code, message, validationErrors is null ? null : new Dictionary<string, string[]>(validationErrors)));
}

public sealed class Result<T> : Result
{
    private Result(bool isSuccess, T? value, Error? error)
        : base(isSuccess, error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(string code, string message, IDictionary<string, string[]>? validationErrors = null) =>
        new(false, default, new Error(code, message, validationErrors is null ? null : new Dictionary<string, string[]>(validationErrors)));
}

