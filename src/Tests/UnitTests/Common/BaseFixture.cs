using Bogus;

namespace UltracarInventoryApi.UnitTests.Common;

public abstract class BaseFixture
{
    public Faker Faker { get; } = new("pt_BR");
    public DateTimeOffset Now { get; } = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
}
