using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using UltracarInventoryApi.Application.Dtos.Parts;
using Xunit;

namespace UltracarInventoryApi.IntegrationTests.Application.Reservation.CreateReservation;

[Collection(nameof(CreateReservationConcurrencyTestFixture))]
public class CreateReservationConcurrencyTest
{
    private readonly CreateReservationConcurrencyTestFixture _fixture;

    public CreateReservationConcurrencyTest(CreateReservationConcurrencyTestFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = nameof(AllowOnlyFiveOfTwentyParallelReservationsWithStockOfFive))]
    [Trait("Integration/Application", "CreateReservation - R2")]
    [Trait("Category", "Integration")]
    public async Task AllowOnlyFiveOfTwentyParallelReservationsWithStockOfFive()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var partId = await _fixture.CreatePartAsync(stock: 5, timeout.Token);
        var requests = Enumerable.Range(0, 20)
            .Select(_ => _fixture.GetReservationRequest(partId))
            .ToArray();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = requests.Select(async request =>
        {
            using (request)
            {
                await start.Task.WaitAsync(timeout.Token);
                using var response = await _fixture.Client.SendAsync(request, timeout.Token);
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: timeout.Token);
                return (response.StatusCode, Body: body);
            }
        }).ToArray();

        // All twenty requests wait at the same gate before competing for the stock.
        start.SetResult();
        var responses = await Task.WhenAll(tasks);

        using (new AssertionScope())
        {
            responses.Should().HaveCount(20);
            responses.Count(response => response.StatusCode == HttpStatusCode.Created).Should().Be(5,
                "only five units are available; received statuses: {0}",
                string.Join(", ", responses.Select(response => (int)response.StatusCode)));
            responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).Should().Be(15);
        }

        var created = responses.Where(response => response.StatusCode == HttpStatusCode.Created).ToArray();
        created.Select(response => response.Body.GetProperty("id").GetInt64())
            .Should().OnlyHaveUniqueItems();
        foreach (var response in responses.Where(response => response.StatusCode == HttpStatusCode.Conflict))
            response.Body.GetProperty("code").GetString().Should().Be("INSUFFICIENT_STOCK");

        using var balanceResponse = await _fixture.Client.GetAsync($"/api/v1/pecas/{partId}/saldo", timeout.Token);
        balanceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var balance = await balanceResponse.Content.ReadFromJsonAsync<PartBalanceResponseDto>(
            cancellationToken: timeout.Token);

        balance.Should().NotBeNull();
        using (new AssertionScope())
        {
            balance!.PartId.Should().Be(partId);
            balance.PhysicalStock.Should().Be(5);
            balance.ReservedStock.Should().Be(5);
            balance.AvailableStock.Should().Be(0);
            (await _fixture.CountReservationsAsync(timeout.Token)).Should().Be(5);
        }
    }
}
