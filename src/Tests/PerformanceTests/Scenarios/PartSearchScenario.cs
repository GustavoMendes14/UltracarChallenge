using NBomber.Contracts;
using NBomber.CSharp;
using UltracarInventoryApi.PerformanceTests.Configuration;

namespace UltracarInventoryApi.PerformanceTests.Scenarios;

internal static class PartSearchScenario
{
    public const string Name = "get_pecas_busca_vazia";

    public static ScenarioProps Create(HttpClient client) =>
        Scenario.Create(Name, context => PartSearchRequest.ExecuteAsync(
            client, context.Random.Next(1, PerformanceSettings.Workshops + 1)))
        .WithWarmUpDuration(PerformanceSettings.WarmUp)
        .WithLoadSimulations(Simulation.KeepConstant(
            copies: PerformanceSettings.Users, during: PerformanceSettings.Duration));
}
