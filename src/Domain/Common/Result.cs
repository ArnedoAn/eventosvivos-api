namespace EventosVivos.Domain.Common;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new("", "");
}

public class Result
{
    protected Result(bool ok, Error error)
    {
        if (ok && error != Error.None) throw new InvalidOperationException();
        if (!ok && error == Error.None) throw new InvalidOperationException();
        IsSuccess = ok; Error = error;
    }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }
    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error e) => new(false, e);
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error e) => new(default!, false, e);
}

public sealed class Result<T> : Result
{
    private readonly T _value;
    internal Result(T value, bool ok, Error error) : base(ok, error) => _value = value;
    public T Value => IsSuccess ? _value : throw new InvalidOperationException("No value on failure.");
    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error e) => Failure<T>(e);
}
