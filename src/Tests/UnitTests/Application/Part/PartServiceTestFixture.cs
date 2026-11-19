using Moq;
using UltracarInventoryApi.Application.Dtos.Parts;
using UltracarInventoryApi.Application.Interfaces.Persistence;
using UltracarInventoryApi.UnitTests.Common;
using Xunit;

namespace UltracarInventoryApi.UnitTests.Application.Part;

[CollectionDefinition(nameof(PartServiceTestFixture))]
public class PartServiceTestFixtureCollection : ICollectionFixture<PartServiceTestFixture> { }

public class PartServiceTestFixture : BaseFixture
{
    public Mock<IPartRepository> GetRepositoryMock() => new();
    public PartResponseDto GetPart(long id) =>
        new(id, $"PEC-{id}", Faker.Commerce.ProductName(), 5, 2, 3);
}
