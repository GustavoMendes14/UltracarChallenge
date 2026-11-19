using Xunit;

namespace UltracarInventoryApi.PerformanceTests.Infrastructure;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PerformanceFactAttribute : FactAttribute
{
    public PerformanceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_PERFORMANCE_TESTS") != "1")
            Skip = "Carga desabilitada. Para criar o ambiente temporário e executar D1/D2, defina RUN_PERFORMANCE_TESTS=1 e disponibilize Docker.";
    }
}
