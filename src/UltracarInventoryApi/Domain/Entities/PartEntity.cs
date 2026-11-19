using UltracarInventoryApi.Domain.Exceptions;

namespace UltracarInventoryApi.Domain.Entities
{
    public class PartEntity
    {
        public long Id { get; private set; }
        public long WorkshopId { get; private set; }
        public string Code { get; private set; } = null!;
        public string Description { get; private set; } = null!;
        public int PhysicalStock { get; private set; }

        private PartEntity()
        {
        }

        public static PartEntity Create(
            long workshopId,
            string code,
            string description,
            int physicalStock)
        {
            if (workshopId <= 0)
            {
                throw new DomainException(
                    "invalid_workshop_id",
                    "Workshop ID must be positive.");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new DomainException(
                    "invalid_part_code",
                    "Part code is required.");
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                throw new DomainException(
                    "invalid_part_description",
                    "Part description is required.");
            }

            if (physicalStock < 0)
            {
                throw new DomainException(
                    "invalid_physical_stock",
                    "Physical stock cannot be negative.");
            }

            return new PartEntity
            {
                WorkshopId = workshopId,
                Code = code.Trim(),
                Description = description.Trim(),
                PhysicalStock = physicalStock
            };
        }

        public void EnsurePhysicalStockAvailable(int quantity)
        {
            if (quantity <= 0)
            {
                throw new DomainException(
                    "invalid_quantity",
                    "Quantity must be positive.");
            }

            if (PhysicalStock < quantity)
            {
                throw new DomainException(
                    "insufficient_physical_stock",
                    "Physical stock is insufficient.");
            }
        }

        public void DecreasePhysicalStock(int quantity)
        {
            EnsurePhysicalStockAvailable(quantity);

            PhysicalStock -= quantity;
        }
    }
}