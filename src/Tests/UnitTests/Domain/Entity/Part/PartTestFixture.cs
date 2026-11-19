using UltracarInventoryApi.Domain.Entities;
using UltracarInventoryApi.UnitTests.Common;
using Xunit;

namespace UltracarInventoryApi.UnitTests.Domain.Entity.Part;

[CollectionDefinition(nameof(PartTestFixture))]
public class PartTestFixtureCollection : ICollectionFixture<PartTestFixture> { }

public class PartTestFixture : BaseFixture
{
    public PartEntity GetValidPart(int stock = 5) =>
        PartEntity.Create(42, Faker.Random.AlphaNumeric(8), Faker.Commerce.ProductName(), stock);
}
