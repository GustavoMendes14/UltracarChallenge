using UltracarInventoryApi.Domain.Exceptions;

namespace UltracarInventoryApi.Domain.ValueObjects;

public sealed record ReservationLine
{
    public long PartId { get; }
    public int Quantity { get; }

    public ReservationLine(long partId, int quantity)
    {
        if (partId <= 0)
        {
            throw new DomainException(
                "invalid_part_id",
                "Part ID must be positive.");
        }

        if (quantity <= 0)
        {
            throw new DomainException(
                "invalid_quantity",
                "Quantity must be positive.");
        }

        PartId = partId;
        Quantity = quantity;
    }
}