using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using UltracarInventoryApi.Application.Common.Results;
using UltracarInventoryApi.Domain.Enums;
using UltracarInventoryApi.Utils.Extensions;
using Xunit;

namespace UltracarInventoryApi.UnitTests.Presentation;

public class ResultExtensionsTest
{
    private sealed class TestController : ControllerBase { }

    [Theory(DisplayName = nameof(ReturnProblemDetailsWithBusinessCode))]
    [Trait("Presentation", "Errors - R7")]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.UnprocessableEntity, 422)]
    public void ReturnProblemDetailsWithBusinessCode(ErrorType type, int status)
    {
        var details = new[] { new { partId = 10L, requestedQuantity = 2, availableStock = 0 } };
        var error = new Error("BUSINESS_ERROR", "Business rule failed.", type, details);

        var result = Result<string>.Failure(error).ToActionResult(new TestController());

        var response = result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(status);
        response.ContentTypes.Should().Contain("application/problem+json");
        var problem = response.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(status);
        problem.Title.Should().NotBeNullOrWhiteSpace();
        problem.Detail.Should().Be(error.Message);
        problem.Extensions["code"].Should().Be(error.Code);
        problem.Extensions["details"].Should().BeEquivalentTo(details);
    }

    [Theory(DisplayName = nameof(ReturnSuccessWithExpectedHttpStatus))]
    [Trait("Presentation", "Reservation - Response")]
    [InlineData(200)]
    [InlineData(201)]
    public void ReturnSuccessWithExpectedHttpStatus(int status)
    {
        var result = Result<string>.Success("reservation").ToActionResult(new TestController(), status);

        var response = result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(status);
        response.Value.Should().Be("reservation");
    }
}
