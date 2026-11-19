using FluentAssertions;
using UltracarInventoryApi.Domain.Exceptions;
using UltracarInventoryApi.Domain.ValueObjects;
using Xunit;

namespace UltracarInventoryApi.UnitTests.Domain.ValueObject.ReservationLine;

public class ReservationLineTest
{
    [Theory(DisplayName = nameof(RejectNonPositivePartOrQuantity))]
    [Trait("Domain", "ReservationLine - Validation")]
    [InlineData(0, 1, "invalid_part_id")]
    [InlineData(-1, 1, "invalid_part_id")]
    [InlineData(10, 0, "invalid_quantity")]
    [InlineData(10, -1, "invalid_quantity")]
    public void RejectNonPositivePartOrQuantity(long partId, int quantity, string code)
    {
        Action action = () => new UltracarInventoryApi.Domain.ValueObjects.ReservationLine(partId, quantity);

        action.Should().Throw<DomainException>().Which.Code.Should().Be(code);
    }
}
