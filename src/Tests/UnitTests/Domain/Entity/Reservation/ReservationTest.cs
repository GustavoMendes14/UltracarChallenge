using FluentAssertions;
using UltracarInventoryApi.Domain.Entities;
using UltracarInventoryApi.Domain.Enums;
using UltracarInventoryApi.Domain.Exceptions;
using UltracarInventoryApi.Domain.ValueObjects;
using Xunit;

namespace UltracarInventoryApi.UnitTests.Domain.Entity.Reservation;

[Collection(nameof(ReservationTestFixture))]
public class ReservationTest
{
    private readonly ReservationTestFixture _fixture;
    public ReservationTest(ReservationTestFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = nameof(InstantiateActiveReservationWithThirtyMinuteLifetime))]
    [Trait("Domain", "Reservation - R4")]
    public void InstantiateActiveReservationWithThirtyMinuteLifetime()
    {
        var reservation = _fixture.GetValidReservation();

        reservation.WorkshopId.Should().Be(42);
        reservation.UserId.Should().Be(1001);
        reservation.ServiceOrderId.Should().StartWith("OS-");
        reservation.Status.Should().Be(ReservationStatus.Active);
        reservation.CreatedAt.Should().Be(_fixture.Now);
        reservation.ExpiresAt.Should().Be(_fixture.Now.AddMinutes(30));
        reservation.Items.Select(item => (item.PartId, item.Quantity)).Should().Equal((10L, 2), (11L, 1));
        reservation.Items.Should().OnlyContain(item => item.WorkshopId == 42);
    }

    [Theory(DisplayName = nameof(OnlyActiveUnexpiredReservationsReserveStock))]
    [Trait("Domain", "Reservation - R4")]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1799, true)]
    [InlineData(1800, false)]
    [InlineData(1801, false)]
    public void OnlyActiveUnexpiredReservationsReserveStock(int seconds, bool expected)
    {
        var reservation = _fixture.GetValidReservation();

        reservation.IsActiveAt(_fixture.Now.AddSeconds(seconds)).Should().Be(expected);
    }

    [Theory(DisplayName = nameof(ExpireOnlyAtOrAfterThirtyMinutes))]
    [Trait("Domain", "Reservation - R4")]
    [InlineData(1799, false)]
    [InlineData(1800, true)]
    [InlineData(1801, true)]
    public void ExpireOnlyAtOrAfterThirtyMinutes(int seconds, bool expected)
    {
        var reservation = _fixture.GetValidReservation();

        var expired = reservation.TryExpire(_fixture.Now.AddSeconds(seconds));

        expired.Should().Be(expected);
        reservation.Status.Should().Be(expected ? ReservationStatus.Expired : ReservationStatus.Active);
    }

    [Theory(DisplayName = nameof(TransitionActiveReservation))]
    [Trait("Domain", "Reservation - R5")]
    [InlineData(true, ReservationStatus.Confirmed)]
    [InlineData(false, ReservationStatus.Cancelled)]
    public void TransitionActiveReservation(bool confirm, ReservationStatus expected)
    {
        var reservation = _fixture.GetValidReservation();

        var changed = confirm ? reservation.Confirm(_fixture.Now.AddMinutes(1)) : reservation.Cancel(_fixture.Now.AddMinutes(1));

        changed.Should().BeTrue();
        reservation.Status.Should().Be(expected);
        reservation.IsActiveAt(_fixture.Now.AddMinutes(2)).Should().BeFalse();
    }

    [Theory(DisplayName = nameof(RejectTransitionAtOrAfterExpiration))]
    [Trait("Domain", "Reservation - R4 R5")]
    [InlineData(true, 1800)]
    [InlineData(false, 1800)]
    [InlineData(true, 1801)]
    [InlineData(false, 1801)]
    public void RejectTransitionAtOrAfterExpiration(bool confirm, int seconds)
    {
        var reservation = _fixture.GetValidReservation();

        Action action = () =>
        {
            if (confirm) reservation.Confirm(_fixture.Now.AddSeconds(seconds));
            else reservation.Cancel(_fixture.Now.AddSeconds(seconds));
        };

        action.Should().Throw<DomainException>().WithMessage("The reservation has expired.")
            .Which.Code.Should().Be("reservation_expired");
        reservation.Status.Should().Be(ReservationStatus.Active);
    }

    [Theory(DisplayName = nameof(RejectEveryTransitionFromTerminalStatus))]
    [Trait("Domain", "Reservation - R5")]
    [InlineData(ReservationStatus.Confirmed, true)]
    [InlineData(ReservationStatus.Confirmed, false)]
    [InlineData(ReservationStatus.Cancelled, true)]
    [InlineData(ReservationStatus.Cancelled, false)]
    [InlineData(ReservationStatus.Expired, true)]
    [InlineData(ReservationStatus.Expired, false)]
    public void RejectEveryTransitionFromTerminalStatus(ReservationStatus status, bool confirm)
    {
        var reservation = _fixture.GetValidReservation();
        if (status == ReservationStatus.Confirmed) reservation.Confirm(_fixture.Now);
        else if (status == ReservationStatus.Cancelled) reservation.Cancel(_fixture.Now);
        else reservation.TryExpire(reservation.ExpiresAt);

        Action action = () =>
        {
            if (confirm) reservation.Confirm(reservation.ExpiresAt);
            else reservation.Cancel(reservation.ExpiresAt);
        };

        action.Should().Throw<DomainException>().Which.Code.Should().Be(
            status == ReservationStatus.Expired ? "reservation_expired" : "invalid_reservation_transition");
        reservation.TryExpire(reservation.ExpiresAt.AddMinutes(1)).Should().BeFalse();
        reservation.Status.Should().Be(status);
    }

    [Fact(DisplayName = nameof(RejectDuplicateParts))]
    [Trait("Domain", "Reservation - Validation")]
    public void RejectDuplicateParts()
    {
        Action action = () => ReservationEntity.Create(42, "OS-1", 1001,
            new[] { new ReservationLine(10, 1), new ReservationLine(10, 2) }, _fixture.Now);

        action.Should().Throw<DomainException>().Which.Code.Should().Be("duplicate_part");
    }
}
