using Bogus;

namespace UltracarInventoryApi.IntegrationTests.Base;

public abstract class BaseFixture
{
    public Faker Faker { get; } = new("pt_BR");
}
