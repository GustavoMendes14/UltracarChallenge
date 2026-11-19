namespace UltracarInventoryApi.Application.Common.Results;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error is not null)
            throw new ArgumentException(
                "A successful result cannot contain an error.");

        if (!isSuccess && error is null)
            throw new ArgumentException(
                "A failed result must contain an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success()
        => new(true, null);

    public static Result Failure(Error error)
        => new(false, error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            "Cannot access the value of a failed result.");

    private Result(T? value, bool isSuccess, Error? error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public static Result<T> Success(T value)
        => new(value, true, null);

    public static new Result<T> Failure(Error error)
        => new(default, false, error);
}