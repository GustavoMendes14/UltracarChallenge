using UltracarInventoryApi.Domain.ValueObjects;

namespace UltracarInventoryApi.Domain.Entities;

public sealed class ReservationItem
{
    public long Id { get; private set; }
    public long ReservationId { get; private set; }
    public long WorkshopId { get; private set; }
    public long PartId { get; private set; }
    public int Quantity { get; private set; }

    private ReservationItem()
    {
    }

    internal ReservationItem(
        long workshopId,
        ReservationLine line)
    {
        WorkshopId = workshopId;
        PartId = line.PartId;
        Quantity = line.Quantity;
    }
}