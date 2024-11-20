using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace Chat.Utils.ResultPattern;

public record Error(string Message, ErrorType Type)
{
    public static Error InvalidArgument(string message)
        => new(message, ErrorType.InvalidArgument);

    public static Error NotFound(string message)
        => new(message, ErrorType.NotFound);

    public static Error Unauthorized(string message)
       => new(message, ErrorType.Unauthorized);

    public static Error InternalServer(string message)
       => new(message, ErrorType.InternalServer);

    public static implicit operator ActionResult(Error error) => Result.Failure(error);
}

public record ErrorResponse
{
    public ErrorResponse(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        Message = error.Message;
        Type = error.Type;
    }

    public ErrorResponse()
    {
        Message = string.Empty;
        Type = ErrorType.InternalServer;
    }

    public string Message { get; set; }

    public ErrorType Type { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ErrorType TypeDescription => Type;

    public static explicit operator ErrorResponse(Error error)
        => new(error: error);
}

public enum ErrorType
{
    InvalidArgument,
    NotFound,
    Unauthorized,
    InternalServer,
}
