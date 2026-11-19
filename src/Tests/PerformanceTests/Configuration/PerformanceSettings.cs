namespace UltracarInventoryApi.PerformanceTests.Configuration;

internal sealed record PerformanceSettings(string ApiDirectory, string RunLabel, string ReportFolder)
{
    public const int Users = 20;
    public const int Workshops = 50;
    public const int PartsPerWorkshop = 4_000;
    public const int PageSize = 20;
    public const double MaxP95Ms = 150;
    public static readonly TimeSpan WarmUp = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(3);

    public static PerformanceSettings FromEnvironment()
    {
        var label = Environment.GetEnvironmentVariable("PERF_RUN_LABEL") ?? "baseline";
        if (label.Length is < 1 or > 80
            || label.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            throw new InvalidOperationException("PERF_RUN_LABEL aceita de 1 a 80 letras ASCII, números, hífen ou sublinhado.");

        var reportRoot = Environment.GetEnvironmentVariable("PERF_REPORTS_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "reports");
        var folder = Path.GetFullPath(Path.Combine(reportRoot,
            $"{label}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"));
        return new PerformanceSettings(FindApiDirectory(), label, folder);
    }

    private static string FindApiDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("PERF_API_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(configured))
            return ValidateApiDirectory(Path.GetFullPath(configured));

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "UltracarInventoryApi");
            if (File.Exists(Path.Combine(candidate, "UltracarInventoryApi.csproj")))
                return ValidateApiDirectory(candidate);
        }
        throw new InvalidOperationException("Projeto da API não encontrado. Execute no checkout do repositório ou defina PERF_API_DIRECTORY com a pasta da API.");
    }

    private static string ValidateApiDirectory(string directory)
    {
        if (!File.Exists(Path.Combine(directory, "UltracarInventoryApi.csproj"))
            || !File.Exists(Path.Combine(directory, "Dockerfile")))
            throw new InvalidOperationException("PERF_API_DIRECTORY deve conter UltracarInventoryApi.csproj e Dockerfile.");
        return directory;
    }
}
