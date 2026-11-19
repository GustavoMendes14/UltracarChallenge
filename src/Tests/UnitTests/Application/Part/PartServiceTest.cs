using FluentAssertions;
using Moq;
using UltracarInventoryApi.Application.Dtos.Parts;
using UltracarInventoryApi.Application.Services;
using UltracarInventoryApi.Domain.Enums;
using Xunit;

namespace UltracarInventoryApi.UnitTests.Application.Part;

[Collection(nameof(PartServiceTestFixture))]
public class PartServiceTest
{
    private readonly PartServiceTestFixture _fixture;
    public PartServiceTest(PartServiceTestFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = nameof(ReturnBalanceWithinWorkshop))]
    [Trait("Application", "Part - R6")]
    public async Task ReturnBalanceWithinWorkshop()
    {
        var repositoryMock = _fixture.GetRepositoryMock();
        var expected = new PartBalanceResponseDto(10, 5, 2, 3);
        using var cancellation = new CancellationTokenSource();
        repositoryMock.Setup(x => x.GetBalanceAsync(42, 10, cancellation.Token)).ReturnsAsync(expected);
        var service = new PartService(repositoryMock.Object);

        var output = await service.GetBalanceAsync(42, 10, cancellation.Token);

        output.IsSuccess.Should().BeTrue();
        output.Value.Should().BeEquivalentTo(expected);
        repositoryMock.Verify(x => x.GetBalanceAsync(42, 10, cancellation.Token), Times.Once);
    }

    [Fact(DisplayName = nameof(ReturnNotFoundForPartOutsideWorkshop))]
    [Trait("Application", "Part - R6")]
    public async Task ReturnNotFoundForPartOutsideWorkshop()
    {
        var repositoryMock = _fixture.GetRepositoryMock();
        repositoryMock.Setup(x => x.GetBalanceAsync(42, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PartBalanceResponseDto?)null);
        var service = new PartService(repositoryMock.Object);

        var output = await service.GetBalanceAsync(42, 10);

        output.Error!.Code.Should().Be("PART_NOT_FOUND");
        output.Error.Type.Should().Be(ErrorType.NotFound);
        repositoryMock.Verify(x => x.GetBalanceAsync(42, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = nameof(SearchTwentyPartsAndContinueUsingCursor))]
    [Trait("Application", "Part - Search")]
    public async Task SearchTwentyPartsAndContinueUsingCursor()
    {
        var repositoryMock = _fixture.GetRepositoryMock();
        var parts = Enumerable.Range(1, 21).Select(id => _fixture.GetPart(id)).ToArray();
        repositoryMock.Setup(x => x.SearchAsync(42, "filtro", null, 21, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parts);
        repositoryMock.Setup(x => x.SearchAsync(42, "filtro", 20, 21, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { parts[20] });
        var service = new PartService(repositoryMock.Object);

        var firstPage = await service.SearchAsync(42, " filtro ", null, 20);
        var secondPage = await service.SearchAsync(42, "filtro", firstPage.Value.NextCursor, 20);

        firstPage.Value.Items.Should().HaveCount(20);
        firstPage.Value.Items.Select(item => item.Id).Should().Equal(Enumerable.Range(1, 20).Select(id => (long)id));
        firstPage.Value.NextCursor.Should().NotBeNullOrEmpty();
        firstPage.Value.Items.Should().OnlyContain(item => item.AvailableStock == 3);
        secondPage.Value.Items.Should().ContainSingle().Which.Id.Should().Be(21);
        secondPage.Value.NextCursor.Should().BeNull();
        repositoryMock.Verify(x => x.SearchAsync(42, "filtro", 20, 21, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName = nameof(RejectCursorFromDifferentWorkshopOrSearch))]
    [Trait("Application", "Part - R6 Search")]
    [InlineData(43, "filtro")]
    [InlineData(42, "pastilha")]
    public async Task RejectCursorFromDifferentWorkshopOrSearch(long workshopId, string term)
    {
        var repositoryMock = _fixture.GetRepositoryMock();
        repositoryMock.Setup(x => x.SearchAsync(42, "filtro", null, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _fixture.GetPart(10), _fixture.GetPart(11) });
        var service = new PartService(repositoryMock.Object);
        var firstPage = await service.SearchAsync(42, "filtro", null, 1);
        repositoryMock.Invocations.Clear();

        var output = await service.SearchAsync(workshopId, term, firstPage.Value.NextCursor, 1);

        output.Error!.Code.Should().Be("INVALID_QUERY");
        output.Error.Type.Should().Be(ErrorType.Validation);
        repositoryMock.VerifyNoOtherCalls();
    }

    [Theory(DisplayName = nameof(RejectMalformedCursor))]
    [Trait("Application", "Part - Search")]
    [InlineData("")]
    [InlineData("***")]
    [InlineData("bnVsbA")]
    [InlineData("e30")]
    public async Task RejectMalformedCursor(string cursor)
    {
        var repositoryMock = _fixture.GetRepositoryMock();
        var service = new PartService(repositoryMock.Object);

        var output = await service.SearchAsync(42, null, cursor, 20);

        output.Error!.Code.Should().Be("INVALID_QUERY");
        repositoryMock.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = nameof(ReturnEmptyPageWithoutCursor))]
    [Trait("Application", "Part - Search")]
    public async Task ReturnEmptyPageWithoutCursor()
    {
        var repositoryMock = _fixture.GetRepositoryMock();
        repositoryMock.Setup(x => x.SearchAsync(42, "", null, 21, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PartResponseDto>());
        var service = new PartService(repositoryMock.Object);

        var output = await service.SearchAsync(42, null, null, 20);

        output.Value.Items.Should().BeEmpty();
        output.Value.NextCursor.Should().BeNull();
    }
}
