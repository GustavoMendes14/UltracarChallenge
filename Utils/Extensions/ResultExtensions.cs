using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using UltracarInventoryApi.Application.Common.Results;
using UltracarInventoryApi.Domain.Enums;

namespace UltracarInventoryApi.Utils.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(
        this Result<T> result,
        ControllerBase controller,
        int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return controller.StatusCode(
                successStatusCode,
                result.Value);
        }

        var error = result.Error!;
        var statusCode = error.Type.ToStatusCode();

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = error.Type == ErrorType.Unexpected
                ? "An unexpected error occurred."
                : error.Message,
            Extensions =
            {
                ["code"] = error.Code
            }
        };

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes =
            {
                "application/problem+json"
            }
        };
    }
}