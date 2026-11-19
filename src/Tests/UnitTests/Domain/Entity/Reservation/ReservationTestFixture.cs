using UltracarInventoryApi.Domain.Entities;
using UltracarInventoryApi.Domain.ValueObjects;
using UltracarInventoryApi.UnitTests.Common;
using Xunit;

namespace UltracarInventoryApi.UnitTests.Domain.Entity.Reservation;

[CollectionDefinition(nameof(ReservationTestFixture))]
public class ReservationTestFixtureCollection : ICollectionFixture<ReservationTestFixture> { }

public class ReservationTestFixture : BaseFixture
{
    public ReservationEntity GetValidReservation() => ReservationEntity.Create(
        42, $"OS-{Faker.Random.Number(1, 9999)}", 1001,
        new[] { new ReservationLine(10, 2), new ReservationLine(11, 1) }, Now);
}
