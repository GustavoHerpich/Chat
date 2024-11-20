using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Chat.Utils.ResultPattern;

public static class ResultExtension
{
    private static ActionResult HandleSuccess() => new NoContentResult();
    private static ActionResult HandleSuccess<T>(this T value)
    {
        if (value is null)
            return HandleSuccess();

        return new OkObjectResult(value);
    }
    private static ActionResult HandleError(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Type switch
        {
            ErrorType.InvalidArgument => new BadRequestObjectResult((ErrorResponse)error),
            ErrorType.NotFound => new NotFoundObjectResult((ErrorResponse)error),
            ErrorType.Unauthorized => new UnauthorizedObjectResult((ErrorResponse)error),
            ErrorType.InternalServer => new ObjectResult((ErrorResponse)error) { StatusCode = (int)HttpStatusCode.InternalServerError },
            _ => throw new InvalidOperationException("Não foi possível executar a função")
        };
    }

    public static ActionResult ToActionResult(this Result result)
    {
        return result.Match(
            onSuccess: HandleSuccess,
            onFailure: HandleError);
    }

    public static ActionResult ToActionResult<T>(this Result<T> result)
    {
        return result.Match(
            onSuccess: HandleSuccess,
            onFailure: HandleError);
    }
}
