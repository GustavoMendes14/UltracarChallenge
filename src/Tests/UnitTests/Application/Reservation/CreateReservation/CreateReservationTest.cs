using System.Text.Json;
using FluentAssertions;
using Moq;
using UltracarInventoryApi.Application.Common.Results;
using UltracarInventoryApi.Application.Dtos.Reservation;
using UltracarInventoryApi.Application.Services;
using UltracarInventoryApi.Domain.Entities;
using UltracarInventoryApi.Domain.Enums;
using UltracarInventoryApi.Domain.ValueObjects;
using Xunit;

namespace UltracarInventoryApi.UnitTests.Application.Reservation.CreateReservation;

[Collection(nameof(CreateReservationTestFixture))]
public class CreateReservationTest
{
    private readonly CreateReservationTestFixture _fixture;
    public CreateReservationTest(CreateReservationTestFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = nameof(CreateAllItemsWithoutDecreasingPhysicalStock))]
    [Trait("Application", "CreateReservation - R1")]
    public async Task CreateAllItemsWithoutDecreasingPhysicalStock()
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var partRepositoryMock = _fixture.GetPartRepositoryMock();
        var reservationRepositoryMock = _fixture.GetReservationRepositoryMock();
        var idempotencyRepositoryMock = _fixture.GetIdempotencyRepositoryMock();
        var input = _fixture.GetInput();
        var key = _fixture.GetIdempotencyKey();
        var parts = new[] { _fixture.GetPart(10), _fixture.GetPart(11) };
        idempotencyRepositoryMock.Setup(x => x.TryBeginAsync(42, key,
            _fixture.GetRequestHash(input), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        partRepositoryMock.Setup(x => x.GetByIdsForUpdateAsync(42,
            It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 10, 11 })),
            It.IsAny<CancellationToken>())).ReturnsAsync(parts);
        reservationRepositoryMock.Setup(x => x.GetActiveItemsByPartIdsAsync(42,
            It.IsAny<IReadOnlyCollection<long>>(), _fixture.Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ReservationItem>());
        reservationRepositoryMock.Setup(x => x.AddAsync(It.IsAny<ReservationEntity>(),
            It.IsAny<CancellationToken>())).ReturnsAsync((ReservationEntity reservation, CancellationToken _) => reservation);
        var service = new ReservationService(unitOfWorkMock.Object, partRepositoryMock.Object,
            reservationRepositoryMock.Object, idempotencyRepositoryMock.Object);

        var output = await service.CreateAsync(42, 1001, key, input);

        output.IsSuccess.Should().BeTrue();
        output.Value.ServiceOrderId.Should().Be(input.ServiceOrderId);
        output.Value.Status.Should().Be(ReservationStatus.Active);
        output.Value.CreatedAt.Should().Be(_fixture.Now);
        output.Value.ExpiresAt.Should().Be(_fixture.Now.AddMinutes(30));
        output.Value.Items.Should().BeEquivalentTo(input.Items);
        parts.Should().OnlyContain(part => part.PhysicalStock == 5);
        reservationRepositoryMock.Verify(x => x.AddAsync(
            It.Is<ReservationEntity>(reservation => reservation.WorkshopId == 42 && reservation.UserId == 1001
                && reservation.Items.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
        partRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PartEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        idempotencyRepositoryMock.Verify(x => x.CompleteAsync(42, key,
            It.Is<string>(json => json.Contains(input.ServiceOrderId)), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = nameof(RejectEntireReservationAndReportEveryShortage))]
    [Trait("Application", "CreateReservation - R1")]
    public async Task RejectEntireReservationAndReportEveryShortage()
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var partRepositoryMock = _fixture.GetPartRepositoryMock();
        var reservationRepositoryMock = _fixture.GetReservationRepositoryMock();
        var idempotencyRepositoryMock = _fixture.GetIdempotencyRepositoryMock();
        var input = new CreateReservationRequestDto
        {
            ServiceOrderId = "OS-1",
            Items = new() { new() { PartId = 10, Quantity = 4 }, new() { PartId = 11, Quantity = 3 },
                new() { PartId = 12, Quantity = 1 } }
        };
        var activeReservation = ReservationEntity.Create(42, "OS-previous", 1001,
            new[] { new ReservationLine(10, 2), new ReservationLine(11, 4) }, _fixture.Now);
        var anotherActiveReservation = ReservationEntity.Create(42, "OS-another", 1001,
            new[] { new ReservationLine(10, 1) }, _fixture.Now);
        idempotencyRepositoryMock.Setup(x => x.TryBeginAsync(42, It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        partRepositoryMock.Setup(x => x.GetByIdsForUpdateAsync(42,
            It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _fixture.GetPart(10), _fixture.GetPart(11), _fixture.GetPart(12) });
        reservationRepositoryMock.Setup(x => x.GetActiveItemsByPartIdsAsync(42,
            It.IsAny<IReadOnlyCollection<long>>(), _fixture.Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeReservation.Items.Concat(anotherActiveReservation.Items).ToArray());
        var service = new ReservationService(unitOfWorkMock.Object, partRepositoryMock.Object,
            reservationRepositoryMock.Object, idempotencyRepositoryMock.Object);

        var output = await service.CreateAsync(42, 1001, "key", input);

        output.IsFailure.Should().BeTrue();
        output.Error!.Code.Should().Be("INSUFFICIENT_STOCK");
        output.Error.Type.Should().Be(ErrorType.Conflict);
        var details = JsonSerializer.SerializeToElement(output.Error.Details);
        details.GetArrayLength().Should().Be(2);
        details[0].GetProperty("partId").GetInt64().Should().Be(10);
        details[0].GetProperty("requestedQuantity").GetInt32().Should().Be(4);
        details[0].GetProperty("availableStock").GetInt64().Should().Be(2);
        details[1].GetProperty("partId").GetInt64().Should().Be(11);
        details[1].GetProperty("availableStock").GetInt64().Should().Be(1);
        reservationRepositoryMock.Verify(x => x.AddAsync(It.IsAny<ReservationEntity>(),
            It.IsAny<CancellationToken>()), Times.Never);
        partRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PartEntity>(),
            It.IsAny<CancellationToken>()), Times.Never);
        idempotencyRepositoryMock.Verify(x => x.CompleteAsync(42, "key",
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = nameof(AllowReservationOfExactAvailableStock))]
    [Trait("Application", "CreateReservation - R1")]
    public async Task AllowReservationOfExactAvailableStock()
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var partRepositoryMock = _fixture.GetPartRepositoryMock();
        var reservationRepositoryMock = _fixture.GetReservationRepositoryMock();
        var idempotencyRepositoryMock = _fixture.GetIdempotencyRepositoryMock();
        var input = new CreateReservationRequestDto
        {
            ServiceOrderId = "OS-last", Items = new() { new() { PartId = 10, Quantity = 2 } }
        };
        var previous = ReservationEntity.Create(42, "OS-previous", 1001,
            new[] { new ReservationLine(10, 3) }, _fixture.Now);
        idempotencyRepositoryMock.Setup(x => x.TryBeginAsync(42, It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        partRepositoryMock.Setup(x => x.GetByIdsForUpdateAsync(42,
            It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _fixture.GetPart() });
        reservationRepositoryMock.Setup(x => x.GetActiveItemsByPartIdsAsync(42,
            It.IsAny<IReadOnlyCollection<long>>(), _fixture.Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(previous.Items.ToArray());
        reservationRepositoryMock.Setup(x => x.AddAsync(It.IsAny<ReservationEntity>(),
            It.IsAny<CancellationToken>())).ReturnsAsync((ReservationEntity reservation, CancellationToken _) => reservation);
        var service = new ReservationService(unitOfWorkMock.Object, partRepositoryMock.Object,
            reservationRepositoryMock.Object, idempotencyRepositoryMock.Object);

        var output = await service.CreateAsync(42, 1001, "key", input);

        output.IsSuccess.Should().BeTrue();
        output.Value.Items.Should().ContainSingle().Which.Quantity.Should().Be(2);
        reservationRepositoryMock.Verify(x => x.AddAsync(It.IsAny<ReservationEntity>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName = nameof(ReplayStoredSuccessOrFailureWithoutReservingAgain))]
    [Trait("Application", "CreateReservation - R3")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReplayStoredSuccessOrFailureWithoutReservingAgain(bool success)
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var partRepositoryMock = _fixture.GetPartRepositoryMock();
        var reservationRepositoryMock = _fixture.GetReservationRepositoryMock();
        var idempotencyRepositoryMock = _fixture.GetIdempotencyRepositoryMock();
        var input = _fixture.GetInput();
        var key = _fixture.GetIdempotencyKey();
        var response = ReservationResponseDto.FromEntity(_fixture.GetReservation());
        var error = new Error("INSUFFICIENT_STOCK", "Insufficient stock.", ErrorType.Conflict,
            new[] { new { partId = 10, availableStock = 0, requestedQuantity = 2 } });
        var stored = _fixture.GetStoredOutcome(input, success
            ? new ReservationService.StoredOutcome(response, null)
            : new ReservationService.StoredOutcome(null, error));
        idempotencyRepositoryMock.Setup(x => x.TryBeginAsync(42, key,
            _fixture.GetRequestHash(input), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        idempotencyRepositoryMock.Setup(x => x.GetAsync(42, key, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        var service = new ReservationService(unitOfWorkMock.Object, partRepositoryMock.Object,
            reservationRepositoryMock.Object, idempotencyRepositoryMock.Object);

        var output = await service.CreateAsync(42, 1001, key, input);

        output.IsSuccess.Should().Be(success);
        if (success) output.Value.Should().BeEquivalentTo(response);
        else
        {
            output.Error!.Code.Should().Be(error.Code);
            output.Error.Type.Should().Be(error.Type);
            output.Error.Message.Should().Be(error.Message);
            JsonSerializer.Serialize(output.Error.Details).Should().Be(JsonSerializer.Serialize(error.Details));
        }
        partRepositoryMock.VerifyNoOtherCalls();
        reservationRepositoryMock.VerifyNoOtherCalls();
        idempotencyRepositoryMock.Verify(x => x.CompleteAsync(It.IsAny<long>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = nameof(RejectSameKeyWithDifferentBody))]
    [Trait("Application", "CreateReservation - R3")]
    public async Task RejectSameKeyWithDifferentBody()
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var partRepositoryMock = _fixture.GetPartRepositoryMock();
        var reservationRepositoryMock = _fixture.GetReservationRepositoryMock();
        var idempotencyRepositoryMock = _fixture.GetIdempotencyRepositoryMock();
        var input = _fixture.GetInput();
        idempotencyRepositoryMock.Setup(x => x.TryBeginAsync(42, "key",
            It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        idempotencyRepositoryMock.Setup(x => x.GetAsync(42, "key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyRecord { RequestHash = "different-body-hash" });
        var service = new ReservationService(unitOfWorkMock.Object, partRepositoryMock.Object,
            reservationRepositoryMock.Object, idempotencyRepositoryMock.Object);

        var output = await service.CreateAsync(42, 1001, "key", input);

        output.IsFailure.Should().BeTrue();
        output.Error!.Code.Should().Be("IDEMPOTENCY_KEY_REUSED");
        output.Error.Type.Should().Be(ErrorType.UnprocessableEntity);
        partRepositoryMock.VerifyNoOtherCalls();
        reservationRepositoryMock.VerifyNoOtherCalls();
        unitOfWorkMock.Verify(x => x.RollbackAsync(CancellationToken.None), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = nameof(RejectPartOutsideWorkshopWithoutPartialReservation))]
    [Trait("Application", "CreateReservation - R1 R6")]
    public async Task RejectPartOutsideWorkshopWithoutPartialReservation()
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var partRepositoryMock = _fixture.GetPartRepositoryMock();
        var reservationRepositoryMock = _fixture.GetReservationRepositoryMock();
        var idempotencyRepositoryMock = _fixture.GetIdempotencyRepositoryMock();
        idempotencyRepositoryMock.Setup(x => x.TryBeginAsync(42, It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        partRepositoryMock.Setup(x => x.GetByIdsForUpdateAsync(42,
            It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _fixture.GetPart(10) });
        var service = new ReservationService(unitOfWorkMock.Object, partRepositoryMock.Object,
            reservationRepositoryMock.Object, idempotencyRepositoryMock.Object);

        var output = await service.CreateAsync(42, 1001, "key", _fixture.GetInput());

        output.Error!.Code.Should().Be("PART_NOT_FOUND");
        output.Error.Type.Should().Be(ErrorType.NotFound);
        reservationRepositoryMock.Verify(x => x.AddAsync(It.IsAny<ReservationEntity>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory(DisplayName = nameof(RejectInvalidInputBeforeOpeningTransaction))]
    [Trait("Application", "CreateReservation - Validation")]
    [MemberData(nameof(CreateReservationTestDataGenerator.GetInvalidInputs),
        MemberType = typeof(CreateReservationTestDataGenerator))]
    public async Task RejectInvalidInputBeforeOpeningTransaction(CreateReservationRequestDto input)
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var service = new ReservationService(unitOfWorkMock.Object, _fixture.GetPartRepositoryMock().Object,
            _fixture.GetReservationRepositoryMock().Object, _fixture.GetIdempotencyRepositoryMock().Object);

        var output = await service.CreateAsync(42, 1001, "key", input);

        output.Error!.Code.Should().Be("INVALID_RESERVATION");
        output.Error.Type.Should().Be(ErrorType.Validation);
        unitOfWorkMock.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = nameof(RollbackWhenPersistenceFails))]
    [Trait("Application", "CreateReservation - R1")]
    public async Task RollbackWhenPersistenceFails()
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var idempotencyRepositoryMock = _fixture.GetIdempotencyRepositoryMock();
        idempotencyRepositoryMock.Setup(x => x.TryBeginAsync(42, It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database failure."));
        var service = new ReservationService(unitOfWorkMock.Object, _fixture.GetPartRepositoryMock().Object,
            _fixture.GetReservationRepositoryMock().Object, idempotencyRepositoryMock.Object);

        Func<Task> action = () => service.CreateAsync(42, 1001, "key", _fixture.GetInput());

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Database failure.");
        unitOfWorkMock.Verify(x => x.RollbackAsync(CancellationToken.None), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = nameof(RepeatRequestAcrossServiceInstancesWithoutCreatingAnotherReservation))]
    [Trait("Application", "CreateReservation - R3")]
    public async Task RepeatRequestAcrossServiceInstancesWithoutCreatingAnotherReservation()
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var partRepositoryMock = _fixture.GetPartRepositoryMock();
        var reservationRepositoryMock = _fixture.GetReservationRepositoryMock();
        var idempotencyRepositoryMock = _fixture.GetIdempotencyRepositoryMock();
        var persisted = _fixture.GetReservation();
        var input = new CreateReservationRequestDto
        {
            ServiceOrderId = persisted.ServiceOrderId,
            Items = new() { new() { PartId = 10, Quantity = 2 }, new() { PartId = 11, Quantity = 1 } }
        };
        string? snapshot = null;
        idempotencyRepositoryMock.SetupSequence(x => x.TryBeginAsync(42, "same-key",
            _fixture.GetRequestHash(input), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true).ReturnsAsync(false);
        idempotencyRepositoryMock.Setup(x => x.CompleteAsync(42, "same-key",
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((long workshopId, string key, string json, CancellationToken token) => snapshot = json)
            .Returns(Task.CompletedTask);
        idempotencyRepositoryMock.Setup(x => x.GetAsync(42, "same-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new IdempotencyRecord
            {
                RequestHash = _fixture.GetRequestHash(input), ResponseJson = snapshot
            });
        partRepositoryMock.Setup(x => x.GetByIdsForUpdateAsync(42,
            It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _fixture.GetPart(10), _fixture.GetPart(11) });
        reservationRepositoryMock.Setup(x => x.GetActiveItemsByPartIdsAsync(42,
            It.IsAny<IReadOnlyCollection<long>>(), _fixture.Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ReservationItem>());
        reservationRepositoryMock.Setup(x => x.AddAsync(It.IsAny<ReservationEntity>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(persisted);
        var firstInstance = new ReservationService(unitOfWorkMock.Object, partRepositoryMock.Object,
            reservationRepositoryMock.Object, idempotencyRepositoryMock.Object);
        var secondInstance = new ReservationService(unitOfWorkMock.Object, partRepositoryMock.Object,
            reservationRepositoryMock.Object, idempotencyRepositoryMock.Object);

        var first = await firstInstance.CreateAsync(42, 1001, "same-key", input);
        persisted.Confirm(_fixture.Now.AddMinutes(1));
        var replay = await secondInstance.CreateAsync(42, 1001, "same-key", input);

        first.IsSuccess.Should().BeTrue();
        replay.IsSuccess.Should().BeTrue();
        replay.Value.Should().BeEquivalentTo(first.Value);
        replay.Value.Id.Should().Be(100);
        replay.Value.Status.Should().Be(ReservationStatus.Active);
        reservationRepositoryMock.Verify(x => x.AddAsync(It.IsAny<ReservationEntity>(),
            It.IsAny<CancellationToken>()), Times.Once);
        partRepositoryMock.Verify(x => x.GetByIdsForUpdateAsync(42,
            It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()), Times.Once);
        idempotencyRepositoryMock.Verify(x => x.CompleteAsync(42, "same-key",
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory(DisplayName = nameof(RejectMissingOrOverlongIdempotencyKey))]
    [Trait("Application", "CreateReservation - R3")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("overlong")]
    public async Task RejectMissingOrOverlongIdempotencyKey(string? key)
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var idempotencyRepositoryMock = _fixture.GetIdempotencyRepositoryMock();
        var service = new ReservationService(unitOfWorkMock.Object, _fixture.GetPartRepositoryMock().Object,
            _fixture.GetReservationRepositoryMock().Object, idempotencyRepositoryMock.Object);
        var invalidKey = key == "overlong" ? new string('a', 201) : key;

        var output = await service.CreateAsync(42, 1001, invalidKey!, _fixture.GetInput());

        output.Error!.Code.Should().Be("INVALID_IDEMPOTENCY_KEY");
        output.Error.Type.Should().Be(ErrorType.Validation);
        idempotencyRepositoryMock.VerifyNoOtherCalls();
        unitOfWorkMock.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = nameof(RejectDuplicatePartsBeforeOpeningTransaction))]
    [Trait("Application", "CreateReservation - Validation")]
    public async Task RejectDuplicatePartsBeforeOpeningTransaction()
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var input = new CreateReservationRequestDto
        {
            ServiceOrderId = "OS-1",
            Items = new() { new() { PartId = 10, Quantity = 1 }, new() { PartId = 10, Quantity = 2 } }
        };
        var service = new ReservationService(unitOfWorkMock.Object, _fixture.GetPartRepositoryMock().Object,
            _fixture.GetReservationRepositoryMock().Object, _fixture.GetIdempotencyRepositoryMock().Object);

        var output = await service.CreateAsync(42, 1001, "key", input);

        output.Error!.Code.Should().Be("DUPLICATE_PART");
        unitOfWorkMock.VerifyNoOtherCalls();
    }
}
