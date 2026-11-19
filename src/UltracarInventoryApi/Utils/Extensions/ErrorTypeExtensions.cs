using UltracarInventoryApi.Domain.Enums;

public static class ErrorTypeExtensions
{
    public static int ToStatusCode(this ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Validation =>
                StatusCodes.Status400BadRequest,

            ErrorType.Forbidden =>
                StatusCodes.Status403Forbidden,

            ErrorType.NotFound =>
                StatusCodes.Status404NotFound,

            ErrorType.Conflict =>
                StatusCodes.Status409Conflict,

            ErrorType.UnprocessableEntity =>
                StatusCodes.Status422UnprocessableEntity,

            ErrorType.TooManyRequests =>
                StatusCodes.Status429TooManyRequests,

            ErrorType.ServiceUnavailable =>
                StatusCodes.Status503ServiceUnavailable,

            _ => StatusCodes.Status500InternalServerError
        };
    }
}