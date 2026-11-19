using System.Text.Json;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;
using UltracarInventoryApi.PerformanceTests.Configuration;
using UltracarInventoryApi.PerformanceTests.Infrastructure;
using UltracarInventoryApi.PerformanceTests.Scenarios;
using Xunit;
using Xunit.Abstractions;

namespace UltracarInventoryApi.PerformanceTests.Tests;

public sealed class PartSearchPerformanceTests(ITestOutputHelper output)
{
    [PerformanceFact(DisplayName = nameof(SearchWithTwentyUsersShouldHaveP95Below150Ms))]
    [Trait("Category", "Performance")]
    public async Task SearchWithTwentyUsersShouldHaveP95Below150Ms()
    {
        var settings = PerformanceSettings.FromEnvironment();
        Directory.CreateDirectory(settings.ReportFolder);
        output.WriteLine($"Relatórios: {settings.ReportFolder}");
        await using var environment = new PerformanceEnvironment(settings.ApiDirectory, output.WriteLine);
        try
        {
            await environment.StartAsync();
            await SeedValidation.ValidateAsync(environment.ConnectionString);
            await environment.CapturePlansAsync(settings.ReportFolder);
            await RunLoadAsync(settings, environment.BaseUrl);
        }
        finally
        {
            await environment.CaptureLogsAsync(settings.ReportFolder);
        }
    }

    private async Task RunLoadAsync(PerformanceSettings settings, Uri baseUrl)
    {
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            MaxConnectionsPerServer = 100
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = baseUrl,
            Timeout = TimeSpan.FromSeconds(10)
        };
        await PartSearchRequest.ValidateWorkshopsAsync(client);

        var startedAt = DateTimeOffset.UtcNow;
        var result = NBomberRunner
            .RegisterScenarios(PartSearchScenario.Create(client))
            .WithReportFolder(settings.ReportFolder)
            .WithReportFileName("busca-vazia")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Md, ReportFormat.Csv)
            .Run();

        var stats = result.ScenarioStats.Single(s => s.ScenarioName == PartSearchScenario.Name);
        var passed = stats.Ok.Request.Count > 0 && stats.Fail.Request.Count == 0
            && stats.Ok.Latency.Percent95 < PerformanceSettings.MaxP95Ms;

        // Write evidence before assertions so failed baselines retain their results.
        var summary = new
        {
            settings.RunLabel,
            StartedAtUtc = startedAt,
            FinishedAtUtc = DateTimeOffset.UtcNow,
            BaseUrl = baseUrl.ToString(),
            PostgresImage = PerformanceEnvironment.PostgresImage,
            ApiBuild = "Dockerfile / Release / Production",
            Endpoint = PartSearchRequest.Path,
            Users = PerformanceSettings.Users,
            Workshops = PerformanceSettings.Workshops,
            WarmUpSeconds = PerformanceSettings.WarmUp.TotalSeconds,
            MeasurementSeconds = PerformanceSettings.Duration.TotalSeconds,
            SuccessfulRequests = stats.Ok.Request.Count,
            FailedRequests = stats.Fail.Request.Count,
            Rps = stats.Ok.Request.RPS,
            P95Ms = stats.Ok.Latency.Percent95,
            Passed = passed,
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            LogicalProcessors = Environment.ProcessorCount
        };
        await File.WriteAllTextAsync(Path.Combine(settings.ReportFolder, "summary.json"),
            JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
        output.WriteLine($"p95={stats.Ok.Latency.Percent95:F2} ms; RPS={stats.Ok.Request.RPS:F2}; falhas={stats.Fail.Request.Count}");

        Assert.True(stats.Ok.Request.Count > 0, "Nenhuma resposta válida foi medida.");
        Assert.Equal(0L, stats.Fail.Request.Count);
        Assert.True(stats.Ok.Latency.Percent95 < PerformanceSettings.MaxP95Ms,
            $"D2 falhou: p95={stats.Ok.Latency.Percent95:F2} ms; esperado < 150 ms. Relatórios: {settings.ReportFolder}");
    }
}
