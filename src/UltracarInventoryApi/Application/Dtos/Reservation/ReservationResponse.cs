using System.Text.Json.Serialization;
using UltracarInventoryApi.Domain.Entities;
using UltracarInventoryApi.Domain.Enums;

namespace UltracarInventoryApi.Application.Dtos.Reservation;

public sealed record ReservationResponseDto(
    [property: JsonPropertyName("id")]
    long Id,

    [property: JsonPropertyName("ordemServicoId")]
    string ServiceOrderId,

    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    ReservationStatus Status,

    [property: JsonPropertyName("criadaEm")]
    DateTimeOffset CreatedAt,

    [property: JsonPropertyName("expiraEm")]
    DateTimeOffset ExpiresAt,

    [property: JsonPropertyName("itens")]
    IReadOnlyList<ReservationItemResponseDto> Items)
{
    public static ReservationResponseDto FromEntity(
        ReservationEntity reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        return new ReservationResponseDto(
            reservation.Id,
            reservation.ServiceOrderId,
            reservation.Status,
            reservation.CreatedAt,
            reservation.ExpiresAt,
            reservation.Items
                .Select(ReservationItemResponseDto.FromEntity)
                .ToArray());
    }
}