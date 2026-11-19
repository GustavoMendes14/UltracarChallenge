using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace UltracarInventoryApi.Application.Dtos.Reservation;

public sealed class CreateReservationRequestDto
{
    [Range(1, long.MaxValue)]
    public long ServiceOrderId { get; init; }

    [Required]
    [MinLength(1)]
    public List<CreateReservationItemRequestDto> Items { get; init; } = new();
}