using UltracarInventoryApi.Application.Dtos.Reservation;

namespace UltracarInventoryApi.UnitTests.Application.Reservation.CreateReservation;

public static class CreateReservationTestDataGenerator
{
    public static IEnumerable<object[]> GetInvalidInputs()
    {
        yield return new object[] { null! };
        yield return new object[] { new CreateReservationRequestDto { ServiceOrderId = "OS-1" } };
        yield return new object[] { new CreateReservationRequestDto { ServiceOrderId = "OS-1", Items = null! } };
        foreach (var serviceOrderId in new[] { "", " ", new string('a', 101) })
            yield return new object[] { new CreateReservationRequestDto
            {
                ServiceOrderId = serviceOrderId,
                Items = new() { new() { PartId = 10, Quantity = 1 } }
            } };
        foreach (var item in new CreateReservationItemRequestDto[]
        {
            null!, new() { PartId = 0, Quantity = 1 }, new() { PartId = -1, Quantity = 1 },
            new() { PartId = 10, Quantity = 0 }, new() { PartId = 10, Quantity = -1 }
        })
            yield return new object[] { new CreateReservationRequestDto
            {
                ServiceOrderId = "OS-1", Items = new() { item }
            } };
    }
}
