using UltracarInventoryApi.UnitTests.Application.Reservation.Common;
using Xunit;

namespace UltracarInventoryApi.UnitTests.Application.Reservation.TransitionReservation;

[CollectionDefinition(nameof(TransitionReservationTestFixture))]
public class TransitionReservationTestFixtureCollection : ICollectionFixture<TransitionReservationTestFixture> { }

public class TransitionReservationTestFixture : ReservationServiceBaseFixture { }
