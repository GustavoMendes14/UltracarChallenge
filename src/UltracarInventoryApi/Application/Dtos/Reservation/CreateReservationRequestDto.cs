using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UltracarInventoryApi.Application.Dtos.Reservation;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateReservationRequestDto
{
    [Required]
    [StringLength(100)]
    [JsonPropertyName("ordemServicoId")]
    public string ServiceOrderId { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    [JsonPropertyName("itens")]
    public List<CreateReservationItemRequestDto> Items { get; init; } = new();
}