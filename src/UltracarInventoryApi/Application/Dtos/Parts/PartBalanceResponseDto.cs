using System.Text.Json.Serialization;
using UltracarInventoryApi.Domain.Entities;

namespace UltracarInventoryApi.Application.Dtos.Parts;

public sealed record PartBalanceResponseDto(
    [property: JsonPropertyName("pecaId")]
    long PartId,

    [property: JsonPropertyName("estoqueFisico")]
    int PhysicalStock,

    [property: JsonPropertyName("estoqueReservado")]
    long ReservedStock,

    [property: JsonPropertyName("saldoDisponivel")]
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