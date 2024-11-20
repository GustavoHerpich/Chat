using Microsoft.AspNetCore.Mvc;

namespace Chat.Utils.ResultPattern;

public record Result(bool IsSuccess, Error? Error)
{
    public bool IsFailure => !IsSuccess;
    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);
    public T Match<T>(Func<T> onSuccess, Func<Error, T> onFailure)
        => IsSuccess switch
        {
            true => onSuccess(),
            false => onFailure(Error!)
        };

    public static implicit operator Result(Error error) => Failure(error);
    public static implicit operator ActionResult(Result result) => result.ToActionResult();
}

public record Result<T>(T? Value, Error? Error, bool IsSuccess) : Result(IsSuccess, Error)
{
    public static Result<T> Success(T value) => new(value, null, true);
    public static new Result<T> Failure(Error error) => new(default, error, false);

    public R Match<R>(Func<T, R> onSuccess, Func<Error, R> onFailure)
        => IsSuccess switch
        {
            true => onSuccess(Value!),
            false => onFailure(Error!)
        };

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
    public static implicit operator ActionResult(Result<T> result) => result.ToActionResult();
    public static implicit operator ActionResult<T>(Result<T> result) => result.ToActionResult();
}