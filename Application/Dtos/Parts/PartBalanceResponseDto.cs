using UltracarInventoryApi.Domain.Entities;

namespace UltracarInventoryApi.Application.Dtos.Parts;

public sealed record PartBalanceResponseDto(
    long PartId,
    int PhysicalStock,
    long ReservedStock,
    long AvailableStock)
{
    public static PartBalanceResponseDto FromEntity(
        PartEntity part,
        long reservedStock)
    {
        ArgumentNullException.ThrowIfNull(part);

        return new PartBalanceResponseDto(
            part.Id,
            part.PhysicalStock,
            reservedStock,
            part.PhysicalStock - reservedStock);
    }
}