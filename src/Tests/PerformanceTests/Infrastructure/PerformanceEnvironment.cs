using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using Npgsql;
using Testcontainers.PostgreSql;

namespace UltracarInventoryApi.PerformanceTests.Infrastructure;

internal sealed class PerformanceEnvironment : IAsyncDisposable
{
    public const string PostgresImage = "postgres:17";
    private const string DatabaseName = "ultracar_performance";
    private const string DatabaseUser = "performance_test";
    private readonly INetwork _network;
    private readonly IFutureDockerImage _image;
    private readonly PostgreSqlContainer _postgres;
    private readonly IContainer _api;
    private readonly Action<string> _log;
    private bool _postgresStarted;
    private bool _apiStarted;

    public PerformanceEnvironment(string apiDirectory, Action<string> log)
    {
        _log = log;
        var id = Guid.NewGuid().ToString("N");
        var password = Guid.NewGuid().ToString("N");
        _network = new NetworkBuilder().WithName($"ultracar-performance-{id}").Build();
        _image = new ImageFromDockerfileBuilder()
            .WithName($"ultracar-performance-api-{id}:latest")
            .WithDockerfileDirectory(apiDirectory)
            .WithDockerfile("Dockerfile")
            .WithCleanUp(true)
            .Build();
        _postgres = new PostgreSqlBuilder(PostgresImage)
            .WithName($"ultracar-performance-db-{id}")
            .WithDatabase(DatabaseName)
            .WithUsername(DatabaseUser)
            .WithPassword(password)
            .WithNetwork(_network)
            .WithNetworkAliases("performance-db")
            .WithResourceMapping(ReadResource("Performance.Schema.sql"), "/tmp/performance-schema.sql")
            .WithResourceMapping(ReadResource("Performance.Seed.sql"), "/tmp/performance-seed.sql")
            .WithResourceMapping(ReadResource("Performance.Explain.sql"), "/tmp/performance-explain.sql")
            .Build();

        var containerConnection = new NpgsqlConnectionStringBuilder
        {
            Host = "performance-db", Port = 5432, Database = DatabaseName,
            Username = DatabaseUser, Password = password
        }.ConnectionString;
        _api = new ContainerBuilder(_image)
            .WithName($"ultracar-performance-api-{id}")
            .WithNetwork(_network)
            .WithPortBinding(8080, true)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
            .WithEnvironment("ASPNETCORE_HTTP_PORTS", "8080")
            .WithEnvironment("ConnectionStrings__Postgres", containerConnection)
            .WithEnvironment("Logging__LogLevel__Default", "Warning")
            .WithEnvironment("Logging__LogLevel__Microsoft.AspNetCore", "Warning")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8080))
            .Build();
    }

    public string ConnectionString => _postgres.GetConnectionString();
    public Uri BaseUrl => new UriBuilder("http", _api.Hostname, _api.GetMappedPublicPort(8080)).Uri;

    public async Task StartAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        _log("Criando rede e PostgreSQL temporários...");
        await _network.CreateAsync(timeout.Token);
        await _postgres.StartAsync(timeout.Token);
        _postgresStarted = true;
        _log("Aplicando schema e seed D1: 200.000 peças e 2.000.000 itens históricos...");
        await ExecuteSqlFileAsync("/tmp/performance-schema.sql", timeout.Token);
        await ExecuteSqlFileAsync("/tmp/performance-seed.sql", timeout.Token);
        _log("Construindo a imagem Release da API a partir do Dockerfile...");
        await _image.CreateAsync(timeout.Token);
        _log("Iniciando API temporária conectada ao PostgreSQL isolado...");
        await _api.StartAsync(timeout.Token);
        _apiStarted = true;
        _log($"API temporária: {BaseUrl}");
    }

    public async Task CapturePlansAsync(string reportFolder)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var result = await ExecuteSqlFileAsync("/tmp/performance-explain.sql", timeout.Token);
        await File.WriteAllTextAsync(Path.Combine(reportFolder, "explain-search.txt"), result.Stdout);
    }

    public async Task CaptureLogsAsync(string reportFolder)
    {
        foreach (var (container, started, name) in new[]
        {
            ((IContainer)_postgres, _postgresStarted, "postgres"), (_api, _apiStarted, "api")
        })
        {
            if (!started) continue;
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var logs = await container.GetLogsAsync(ct: timeout.Token);
                await File.WriteAllTextAsync(Path.Combine(reportFolder, $"{name}.log"),
                    logs.Stdout + Environment.NewLine + logs.Stderr);
            }
            catch (Exception exception)
            {
                _log($"Não foi possível capturar logs de {name}: {exception.GetType().Name}.");
            }
        }
    }

    private async Task<ExecResult> ExecuteSqlFileAsync(string path, CancellationToken cancellationToken)
    {
        var result = await _postgres.ExecAsync(new[]
        {
            "psql", "-X", "-v", "ON_ERROR_STOP=1", "--username", DatabaseUser,
            "--dbname", DatabaseName, "--file", path
        }, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Falha no SQL {path}: {result.Stderr}");
        return result;
    }

    private static byte[] ReadResource(string name)
    {
        using var stream = typeof(PerformanceEnvironment).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Recurso SQL não encontrado: {name}.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Encoding.UTF8.GetBytes(reader.ReadToEnd());
    }

    public async ValueTask DisposeAsync()
    {
        _log("Removendo API, PostgreSQL, dados, imagem temporária da API e rede do teste...");
        var errors = new List<Exception>();
        // Always attempt every cleanup, even if setup or an earlier disposal failed.
        foreach (var resource in new IAsyncDisposable[] { _api, _postgres, _image, _network })
        {
            try { await resource.DisposeAsync(); }
            catch (Exception exception) { errors.Add(exception); }
        }
        if (errors.Count > 0)
            throw new AggregateException("Falha ao remover um ou mais recursos temporários do teste.", errors);
        _log("Ambiente temporário removido. Relatórios preservados.");
    }
}
