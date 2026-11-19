namespace UltracarInventoryApi.Application.Common.Results;

public sealed class IdempotencyRecord
{
    public string RequestHash { get; set; } = string.Empty;

    public string? ResponseJson { get; set; }
}