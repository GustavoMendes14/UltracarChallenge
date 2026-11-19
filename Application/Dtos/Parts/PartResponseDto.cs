using UltracarInventoryApi.Domain.Entities;

namespace UltracarInventoryApi.Application.Dtos.Parts;

public sealed record PartResponseDto(
    long Id,
    string Code,
    string Description,
    int PhysicalStock,
    long ReservedStock,
    long AvailableStock)
    {
    public static PartResponseDto FromEntity(
        PartEntity part,
        long reservedStock)
    {
        ArgumentNullException.ThrowIfNull(part);

        return new PartResponseDto(
            part.Id,
            part.Code,
            part.Description,
            part.PhysicalStock,
            reservedStock,
            part.PhysicalStock - reservedStock);
    }
}