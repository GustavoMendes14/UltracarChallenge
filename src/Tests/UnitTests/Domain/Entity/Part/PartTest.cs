using FluentAssertions;
using UltracarInventoryApi.Application.Dtos.Parts;
using UltracarInventoryApi.Domain.Exceptions;
using Xunit;

namespace UltracarInventoryApi.UnitTests.Domain.Entity.Part;

[Collection(nameof(PartTestFixture))]
public class PartTest
{
    private readonly PartTestFixture _fixture;
    public PartTest(PartTestFixture fixture) => _fixture = fixture;

    [Theory(DisplayName = nameof(DecreasePhysicalStockWithoutNegativeBalance))]
    [Trait("Domain", "Part - Stock")]
    [InlineData(1, 4)]
    [InlineData(5, 0)]
    public void DecreasePhysicalStockWithoutNegativeBalance(int quantity, int expected)
    {
        var part = _fixture.GetValidPart();

        part.DecreasePhysicalStock(quantity);

        part.PhysicalStock.Should().Be(expected);
    }

    [Theory(DisplayName = nameof(RejectInvalidStockDecrease))]
    [Trait("Domain", "Part - Stock")]
    [InlineData(0, "invalid_quantity")]
    [InlineData(-1, "invalid_quantity")]
    [InlineData(6, "insufficient_physical_stock")]
    public void RejectInvalidStockDecrease(int quantity, string code)
    {
        var part = _fixture.GetValidPart();

        Action action = () => part.DecreasePhysicalStock(quantity);

        action.Should().Throw<DomainException>().Which.Code.Should().Be(code);
        part.PhysicalStock.Should().Be(5);
    }

    [Theory(DisplayName = nameof(CalculateAvailableStock))]
    [Trait("Domain", "Part - Balance")]
    [InlineData(0, 5)]
    [InlineData(3, 2)]
    [InlineData(5, 0)]
    public void CalculateAvailableStock(long reserved, long expected)
    {
        var part = _fixture.GetValidPart();

        var balance = PartBalanceResponseDto.FromEntity(part, reserved);
        var searchItem = PartResponseDto.FromEntity(part, reserved);

        balance.PhysicalStock.Should().Be(5);
        balance.ReservedStock.Should().Be(reserved);
        balance.AvailableStock.Should().Be(expected);
        searchItem.AvailableStock.Should().Be(expected);
    }
}
