using EventosVivos.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace EventosVivos.Api.Common;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
            return new OkResult();

        return MapError(result.Error);
    }

    public static IActionResult ToActionResult<T>(this Result<T> result, Func<T, IActionResult> onSuccess)
    {
        if (result.IsSuccess)
            return onSuccess(result.Value);

        return MapError(result.Error);
    }

    public static IActionResult ToCreatedResult<T>(this Result<T> result, string uri)
    {
        if (result.IsSuccess)
            return new CreatedResult(uri, result.Value);

        return MapError(result.Error);
    }

    private static IActionResult MapError(Error error)
    {
        if (error.Code.EndsWith(".notFound", StringComparison.OrdinalIgnoreCase))
            return new NotFoundObjectResult(CreateProblemDetails(error, 404));

        if (error.Code.StartsWith("auth.", StringComparison.OrdinalIgnoreCase))
            return new UnauthorizedObjectResult(CreateProblemDetails(error, 401));

        if (error.Code.Equals("concurrency.conflict", StringComparison.OrdinalIgnoreCase))
            return new ConflictObjectResult(CreateProblemDetails(error, 409));

        var statusCode = error.Code.StartsWith("validation.", StringComparison.OrdinalIgnoreCase)
            ? 400
            : 422;

        return new ObjectResult(CreateProblemDetails(error, statusCode))
        {
            StatusCode = statusCode
        };
    }

    private static ProblemDetails CreateProblemDetails(Error error, int statusCode)
    {
        return new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitleForStatusCode(statusCode),
            Detail = error.Message,
            Type = $"https://httpstatuses.com/{statusCode}"
        };
    }

    private static string GetTitleForStatusCode(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        404 => "Not Found",
        409 => "Conflict",
        422 => "Unprocessable Entity",
        _ => "Error"
    };
}
