using UltracarInventoryApi.Domain.Entities;

namespace UltracarInventoryApi.Application.Dtos.Reservation;

public sealed record ReservationItemResponseDto(
    long PartId,
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