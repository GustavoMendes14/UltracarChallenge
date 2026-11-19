using System.Net;
using System.Text.Json;
using UltracarInventoryApi.PerformanceTests.Scenarios;
using Xunit;

namespace UltracarInventoryApi.PerformanceTests.Tests;

// These checks exercise the load generator's error classification without running load.
public sealed class PartSearchRequestTests
{
    [Theory]
    [Trait("Category", "Harness")]
    [InlineData(400)]
    [InlineData(500)]
    [InlineData(307)]
    public async Task HttpErrorsAndRedirectsMustNotCountAsSuccessfulRequests(int status)
    {
        using var client = CreateClient(_ => new HttpResponseMessage((HttpStatusCode)status));
        var result = await PartSearchRequest.ExecuteAsync(client, 1);
        Assert.True(result.IsError);
        Assert.Equal(status.ToString(), result.StatusCode);
    }

    [Theory]
    [Trait("Category", "Harness")]
    [InlineData("{\"items\":[]}", "INVALID_PAGE")]
    [InlineData("{}", "INVALID_PAGE")]
    [InlineData("[]", "INVALID_PAGE")]
    [InlineData("not json", "INVALID_JSON")]
    public async Task InvalidOrEmptyPagesMustNotPassTheBenchmark(string body, string status)
    {
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body)
        });
        var result = await PartSearchRequest.ExecuteAsync(client, 1);
        Assert.True(result.IsError);
        Assert.Equal(status, result.StatusCode);
    }

    [Fact]
    [Trait("Category", "Harness")]
    public async Task SuccessfulRequestMustUseTheWorkshopHeaderAndConsumeThePage()
    {
        using var client = CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(PartSearchRequest.Path, request.RequestUri!.PathAndQuery);
            Assert.Equal("37", Assert.Single(request.Headers.GetValues("X-Oficina-Id")));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    items = Enumerable.Range(1, 20).Select(id => new { id })
                }))
            };
        });
        var result = await PartSearchRequest.ExecuteAsync(client, 37);
        Assert.False(result.IsError);
        Assert.Equal("200", result.StatusCode);
        Assert.True(result.SizeBytes > 0);
    }

    [Theory]
    [Trait("Category", "Harness")]
    [InlineData(false, "HTTP_ERROR")]
    [InlineData(true, "TIMEOUT")]
    public async Task TransportErrorsMustNotCountAsSuccessfulRequests(bool timeout, string status)
    {
        using var client = CreateClient(_ => throw (timeout
            ? new TaskCanceledException()
            : new HttpRequestException()));
        var result = await PartSearchRequest.ExecuteAsync(client, 1);
        Assert.True(result.IsError);
        Assert.Equal(status, result.StatusCode);
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new StubHandler(respond)) { BaseAddress = new Uri("http://localhost:5087") };

    [Fact]
    [Trait("Category", "Harness")]
    public async Task PreflightMustAcceptTheActualPortugueseApiContractForAllWorkshops()
    {
        var workshops = new HashSet<int>();
        using var client = CreateClient(request =>
        {
            var workshop = int.Parse(Assert.Single(request.Headers.GetValues("X-Oficina-Id")));
            workshops.Add(workshop);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    items = Enumerable.Range(1, 20).Select(index =>
                    {
                        var id = (workshop - 1L) * 4000 + index;
                        return new
                        {
                            id, codigo = $"PERF-{id:D7}", descricao = "Filtro de oleo",
                            estoqueFisico = 200, estoqueReservado = 0, saldoDisponivel = 200
                        };
                    }),
                    nextCursor = "next-page"
                }))
            };
        });
        await PartSearchRequest.ValidateWorkshopsAsync(client);
        Assert.Equal(Enumerable.Range(1, 50), workshops.OrderBy(id => id));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
