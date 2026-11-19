using System.ComponentModel.DataAnnotations;

namespace UltracarInventoryApi.Application.Dtos.Reservation;

public sealed class CreateReservationItemRequestDto
{
    [Range(1, long.MaxValue)]
    public long PartId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}