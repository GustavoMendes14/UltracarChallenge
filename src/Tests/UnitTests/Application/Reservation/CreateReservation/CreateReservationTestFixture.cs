using System.Security.Cryptography;
using System.Text.Json;
using UltracarInventoryApi.Application.Common.Results;
using UltracarInventoryApi.Application.Dtos.Reservation;
using UltracarInventoryApi.Application.Services;
using UltracarInventoryApi.UnitTests.Application.Reservation.Common;
using Xunit;

namespace UltracarInventoryApi.UnitTests.Application.Reservation.CreateReservation;

[CollectionDefinition(nameof(CreateReservationTestFixture))]
public class CreateReservationTestFixtureCollection : ICollectionFixture<CreateReservationTestFixture> { }

public class CreateReservationTestFixture : ReservationServiceBaseFixture
{
    public JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public string GetRequestHash(CreateReservationRequestDto input) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input, JsonOptions)));

    public IdempotencyRecord GetStoredOutcome(CreateReservationRequestDto input, ReservationService.StoredOutcome outcome) =>
        new()
        {
            RequestHash = GetRequestHash(input),
            ResponseJson = JsonSerializer.Serialize(outcome, JsonOptions)
        };
}
