using System.Text.Json.Serialization;
using UltracarInventoryApi.Domain.Entities;

namespace UltracarInventoryApi.Application.Dtos.Parts;

public sealed record PartResponseDto(
    [property: JsonPropertyName("id")]
    long Id,

    [property: JsonPropertyName("codigo")]
    string Code,

    [property: JsonPropertyName("descricao")]
    string Description,

    [property: JsonPropertyName("estoqueFisico")]
    int PhysicalStock,

    [property: JsonPropertyName("estoqueReservado")]
    long ReservedStock,

    [property: JsonPropertyName("saldoDisponivel")]
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