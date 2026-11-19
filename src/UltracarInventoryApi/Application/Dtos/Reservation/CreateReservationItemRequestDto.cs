using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UltracarInventoryApi.Application.Dtos.Reservation;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateReservationItemRequestDto
{
    [Range(1, long.MaxValue)]
    [JsonPropertyName("pecaId")]
    public long PartId { get; init; }

    [Range(1, int.MaxValue)]
    [JsonPropertyName("quantidade")]
    public int Quantity { get; init; }
}