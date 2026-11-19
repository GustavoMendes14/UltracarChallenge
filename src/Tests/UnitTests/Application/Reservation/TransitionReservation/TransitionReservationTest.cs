using FluentAssertions;
using Moq;
using UltracarInventoryApi.Application.Services;
using UltracarInventoryApi.Domain.Entities;
using UltracarInventoryApi.Domain.Enums;
using Xunit;

namespace UltracarInventoryApi.UnitTests.Application.Reservation.TransitionReservation;

[Collection(nameof(TransitionReservationTestFixture))]
public class TransitionReservationTest
{
    private readonly TransitionReservationTestFixture _fixture;
    public TransitionReservationTest(TransitionReservationTestFixture fixture) => _fixture = fixture;

    [Theory(DisplayName = nameof(ConfirmOrCancelActiveReservation))]
    [Trait("Application", "TransitionReservation - R5")]
    [InlineData(true, ReservationStatus.Confirmed)]
    [InlineData(false, ReservationStatus.Cancelled)]
    public async Task ConfirmOrCancelActiveReservation(bool confirm, ReservationStatus expected)
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var partRepositoryMock = _fixture.GetPartRepositoryMock();
        var reservationRepositoryMock = _fixture.GetReservationRepositoryMock();
        var reservation = _fixture.GetReservation();
        var parts = new[] { _fixture.GetPart(10), _fixture.GetPart(11) };
        reservationRepositoryMock.Setup(x => x.GetByIdForUpdateAsync(42, 100,
            It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        partRepositoryMock.Setup(x => x.GetByIdsForUpdateAsync(42,
            It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>())).ReturnsAsync(parts);
        var service = new ReservationService(unitOfWorkMock.Object, partRepositoryMock.Object,
            reservationRepositoryMock.Object, _fixture.GetIdempotencyRepositoryMock().Object);

        var output = confirm ? await service.ConfirmAsync(42, 100) : await service.CancelAsync(42, 100);

        output.IsSuccess.Should().BeTrue();
        output.Value.Status.Should().Be(expected);
        reservation.Status.Should().Be(expected);
        reservation.IsActiveAt(_fixture.Now).Should().BeFalse();
        parts[0].PhysicalStock.Should().Be(confirm ? 3 : 5);
        parts[1].PhysicalStock.Should().Be(confirm ? 4 : 5);
        reservationRepositoryMock.Verify(x => x.UpdateAsync(reservation, It.IsAny<CancellationToken>()), Times.Once);
        partRepositoryMock.Verify(x => x.UpdateAsync(parts[0], It.IsAny<CancellationToken>()),
            confirm ? Times.Once() : Times.Never());
        partRepositoryMock.Verify(x => x.UpdateAsync(parts[1], It.IsAny<CancellationToken>()),
            confirm ? Times.Once() : Times.Never());
        partRepositoryMock.Verify(x => x.GetByIdsForUpdateAsync(42,
            It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()),
            confirm ? Times.Once() : Times.Never());
        unitOfWorkMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory(DisplayName = nameof(ExpireReservationBeforeAttemptedTransition))]
    [Trait("Application", "TransitionReservation - R4 R5")]
    [InlineData(true, 1800)]
    [InlineData(false, 1800)]
    [InlineData(true, 1801)]
    [InlineData(false, 1801)]
    public async Task ExpireReservationBeforeAttemptedTransition(bool confirm, int seconds)
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var partRepositoryMock = _fixture.GetPartRepositoryMock();
        var reservationRepositoryMock = _fixture.GetReservationRepositoryMock();
        var reservation = _fixture.GetReservation();
        var parts = new[] { _fixture.GetPart(10), _fixture.GetPart(11) };
        unitOfWorkMock.Setup(x => x.GetDatabaseTimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_fixture.Now.AddSeconds(seconds));
        reservationRepositoryMock.Setup(x => x.GetByIdForUpdateAsync(42, 100,
            It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        partRepositoryMock.Setup(x => x.GetByIdsForUpdateAsync(42,
            It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>())).ReturnsAsync(parts);
        var service = new ReservationService(unitOfWorkMock.Object, partRepositoryMock.Object,
            reservationRepositoryMock.Object, _fixture.GetIdempotencyRepositoryMock().Object);

        var output = confirm ? await service.ConfirmAsync(42, 100) : await service.CancelAsync(42, 100);

        output.IsFailure.Should().BeTrue();
        output.Error!.Code.Should().Be("RESERVATION_EXPIRED");
        output.Error.Type.Should().Be(ErrorType.Conflict);
        reservation.Status.Should().Be(ReservationStatus.Expired);
        reservation.IsActiveAt(reservation.ExpiresAt).Should().BeFalse();
        parts.Should().OnlyContain(part => part.PhysicalStock == 5);
        partRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PartEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        reservationRepositoryMock.Verify(x => x.UpdateAsync(reservation, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName = nameof(RejectEveryTransitionFromTerminalStatus))]
    [Trait("Application", "TransitionReservation - R5")]
    [InlineData(ReservationStatus.Confirmed, true)]
    [InlineData(ReservationStatus.Confirmed, false)]
    [InlineData(ReservationStatus.Cancelled, true)]
    [InlineData(ReservationStatus.Cancelled, false)]
    [InlineData(ReservationStatus.Expired, true)]
    [InlineData(ReservationStatus.Expired, false)]
    public async Task RejectEveryTransitionFromTerminalStatus(ReservationStatus status, bool confirm)
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var partRepositoryMock = _fixture.GetPartRepositoryMock();
        var reservationRepositoryMock = _fixture.GetReservationRepositoryMock();
        var reservation = _fixture.GetReservation();
        if (status == ReservationStatus.Confirmed) reservation.Confirm(_fixture.Now);
        else if (status == ReservationStatus.Cancelled) reservation.Cancel(_fixture.Now);
        else reservation.TryExpire(reservation.ExpiresAt);
        reservationRepositoryMock.Setup(x => x.GetByIdForUpdateAsync(42, 100,
            It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        var service = new ReservationService(unitOfWorkMock.Object, partRepositoryMock.Object,
            reservationRepositoryMock.Object, _fixture.GetIdempotencyRepositoryMock().Object);

        var output = confirm ? await service.ConfirmAsync(42, 100) : await service.CancelAsync(42, 100);

        output.Error!.Code.Should().Be(status == ReservationStatus.Expired
            ? "RESERVATION_EXPIRED" : "INVALID_RESERVATION_TRANSITION");
        output.Error.Type.Should().Be(ErrorType.Conflict);
        reservation.Status.Should().Be(status);
        partRepositoryMock.VerifyNoOtherCalls();
        reservationRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<ReservationEntity>(),
            It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.RollbackAsync(CancellationToken.None), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory(DisplayName = nameof(ReturnNotFoundForReservationOutsideWorkshop))]
    [Trait("Application", "TransitionReservation - R6")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReturnNotFoundForReservationOutsideWorkshop(bool confirm)
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var partRepositoryMock = _fixture.GetPartRepositoryMock();
        var reservationRepositoryMock = _fixture.GetReservationRepositoryMock();
        reservationRepositoryMock.Setup(x => x.GetByIdForUpdateAsync(42, 100,
            It.IsAny<CancellationToken>())).ReturnsAsync((ReservationEntity?)null);
        var service = new ReservationService(unitOfWorkMock.Object, partRepositoryMock.Object,
            reservationRepositoryMock.Object, _fixture.GetIdempotencyRepositoryMock().Object);

        var output = confirm ? await service.ConfirmAsync(42, 100) : await service.CancelAsync(42, 100);

        output.Error!.Code.Should().Be("RESERVATION_NOT_FOUND");
        output.Error.Type.Should().Be(ErrorType.NotFound);
        partRepositoryMock.VerifyNoOtherCalls();
        unitOfWorkMock.Verify(x => x.RollbackAsync(CancellationToken.None), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = nameof(RejectConfirmationWithoutPartiallyDecreasingPhysicalStock))]
    [Trait("Application", "TransitionReservation - R1 R5")]
    public async Task RejectConfirmationWithoutPartiallyDecreasingPhysicalStock()
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var partRepositoryMock = _fixture.GetPartRepositoryMock();
        var reservationRepositoryMock = _fixture.GetReservationRepositoryMock();
        var reservation = _fixture.GetReservation();
        var parts = new[] { _fixture.GetPart(10), _fixture.GetPart(11, stock: 0) };
        reservationRepositoryMock.Setup(x => x.GetByIdForUpdateAsync(42, 100,
            It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        partRepositoryMock.Setup(x => x.GetByIdsForUpdateAsync(42,
            It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>())).ReturnsAsync(parts);
        var service = new ReservationService(unitOfWorkMock.Object, partRepositoryMock.Object,
            reservationRepositoryMock.Object, _fixture.GetIdempotencyRepositoryMock().Object);

        var output = await service.ConfirmAsync(42, 100);

        output.Error!.Code.Should().Be("INSUFFICIENT_PHYSICAL_STOCK");
        output.Error.Type.Should().Be(ErrorType.Conflict);
        reservation.Status.Should().Be(ReservationStatus.Active);
        parts[0].PhysicalStock.Should().Be(5);
        partRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PartEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        reservationRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<ReservationEntity>(),
            It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.RollbackAsync(CancellationToken.None), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = nameof(RollbackWhenUpdatingConfirmedReservationFails))]
    [Trait("Application", "TransitionReservation - R1 R5")]
    public async Task RollbackWhenUpdatingConfirmedReservationFails()
    {
        var unitOfWorkMock = _fixture.GetUnitOfWorkMock();
        var partRepositoryMock = _fixture.GetPartRepositoryMock();
        var reservationRepositoryMock = _fixture.GetReservationRepositoryMock();
        reservationRepositoryMock.Setup(x => x.GetByIdForUpdateAsync(42, 100,
            It.IsAny<CancellationToken>())).ReturnsAsync(_fixture.GetReservation());
        partRepositoryMock.Setup(x => x.GetByIdsForUpdateAsync(42,
            It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _fixture.GetPart(10), _fixture.GetPart(11) });
        reservationRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<ReservationEntity>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Update failed."));
        var service = new ReservationService(unitOfWorkMock.Object, partRepositoryMock.Object,
            reservationRepositoryMock.Object, _fixture.GetIdempotencyRepositoryMock().Object);

        Func<Task> action = () => service.ConfirmAsync(42, 100);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Update failed.");
        unitOfWorkMock.Verify(x => x.RollbackAsync(CancellationToken.None), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
