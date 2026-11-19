using UltracarInventoryApi.Domain.Enums;

namespace UltracarInventoryApi.Application.Common.Results;

public sealed record Error(
    string Code,
    string Message,
    ErrorType Type
);