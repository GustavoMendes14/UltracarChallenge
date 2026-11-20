namespace UltracarInventoryApi.Domain.Enums;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    UnprocessableEntity,
    TooManyRequests,
    ServiceUnavailable,
    Unexpected

}
