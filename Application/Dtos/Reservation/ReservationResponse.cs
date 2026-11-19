using System.Text.Json.Serialization;
using UltracarInventoryApi.Application.Dtos.Reservation;
using UltracarInventoryApi.Domain.Entities;
using UltracarInventoryApi.Domain.Enums;

public sealed record ReservationResponseDto(
    long Id,
    long ServiceOrderId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    ReservationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<ReservationItemResponseDto> Items)
{
    public static ReservationResponseDto FromEntity(
        ReservationEntity reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var items = reservation.Items
            .Select(item => new ReservationItemResponseDto(
                item.PartId,
                item.Quantity))
            .ToArray();

        return new ReservationResponseDto(
            reservation.Id,
            reservation.ServiceOrderId,
            reservation.Status,
            reservation.CreatedAt,
            reservation.ExpiresAt,
            items);
    }
}