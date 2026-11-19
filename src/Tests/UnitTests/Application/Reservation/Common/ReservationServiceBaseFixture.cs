using Moq;
using UltracarInventoryApi.Application.Dtos.Reservation;
using UltracarInventoryApi.Application.Interfaces.Persistence;
using UltracarInventoryApi.Domain.Entities;
using UltracarInventoryApi.Domain.ValueObjects;
using UltracarInventoryApi.UnitTests.Common;

namespace UltracarInventoryApi.UnitTests.Application.Reservation.Common;

public abstract class ReservationServiceBaseFixture : BaseFixture
{
    public long WorkshopId => 42;
    public long UserId => 1001;
    public long ReservationId => 100;
    public string GetIdempotencyKey() => Faker.Random.Guid().ToString();

    public CreateReservationRequestDto GetInput() => new()
    {
        ServiceOrderId = $"OS-{Faker.Random.Number(1, 9999)}",
        Items = new()
        {
            new() { PartId = 10, Quantity = 2 },
            new() { PartId = 11, Quantity = 1 }
        }
    };

    public PartEntity GetPart(long id = 10, int stock = 5)
    {
        var part = PartEntity.Create(WorkshopId, $"PEC-{id}", Faker.Commerce.ProductName(), stock);
        // Simulates the identity populated by the persistence mapper.
        typeof(PartEntity).GetProperty(nameof(PartEntity.Id))!.SetValue(part, id);
        return part;
    }

    public ReservationEntity GetReservation()
    {
        var reservation = ReservationEntity.Create(WorkshopId, "OS-1532", UserId,
            new[] { new ReservationLine(10, 2), new ReservationLine(11, 1) }, Now);
        typeof(ReservationEntity).GetProperty(nameof(ReservationEntity.Id))!.SetValue(reservation, ReservationId);
        return reservation;
    }

    public Mock<IUnitOfWork> GetUnitOfWorkMock()
    {
        var mock = new Mock<IUnitOfWork>();
        mock.Setup(x => x.GetDatabaseTimeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Now);
        return mock;
    }

    public Mock<IPartRepository> GetPartRepositoryMock() => new();
    public Mock<IReservationRepository> GetReservationRepositoryMock() => new();
    public Mock<IIdempotencyRepository> GetIdempotencyRepositoryMock() => new();
}
