using System.Text.Json.Serialization;
using UltracarInventoryApi.Domain.Entities;

namespace UltracarInventoryApi.Application.Dtos.Reservation;

public sealed record ReservationItemResponseDto(
    [property: JsonPropertyName("pecaId")]
    long PartId,

    [property: JsonPropertyName("quantidade")]
    int Quantity)
{
    public static ReservationItemResponseDto FromEntity(
        ReservationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new ReservationItemResponseDto(
            item.PartId,
            item.Quantity);
    }
}