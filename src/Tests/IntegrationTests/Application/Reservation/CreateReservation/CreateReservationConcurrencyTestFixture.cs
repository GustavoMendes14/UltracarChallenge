using System.Globalization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using UltracarInventoryApi.Application.Dtos.Reservation;
using UltracarInventoryApi.IntegrationTests.Base;
using Xunit;


namespace UltracarInventoryApi.IntegrationTests.Application.Reservation.CreateReservation;

[CollectionDefinition(nameof(CreateReservationConcurrencyTestFixture))]
public class CreateReservationConcurrencyTestFixtureCollection
    : ICollectionFixture<CreateReservationConcurrencyTestFixture> { }

public class CreateReservationConcurrencyTestFixture : BaseFixture, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6")
        .WithDatabase("ultracar_integration")
        .WithUsername("ultracar_test")
        .WithPassword("integration-test-password")
        .Build();

    private InventoryApiFactory? _factory;

    public long WorkshopId => 42;
    public long UserId => 1001;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        try
        {
            await _postgres.StartAsync(timeout.Token);

            await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
            await connection.OpenAsync(timeout.Token);
            await using var schema = typeof(CreateReservationConcurrencyTestFixture).Assembly
                .GetManifestResourceStream("UltracarInventoryApi.IntegrationTests.Base.DatabaseSchema.sql")
                ?? throw new InvalidOperationException("Integration database schema was not embedded.");
            using var reader = new StreamReader(schema);
            await using var command = new NpgsqlCommand(await reader.ReadToEndAsync(timeout.Token), connection);
            await command.ExecuteNonQueryAsync(timeout.Token);

            _factory = new InventoryApiFactory(_postgres.GetConnectionString());
            Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
            Client.DefaultRequestHeaders.Add("X-Oficina-Id", WorkshopId.ToString(CultureInfo.InvariantCulture));
            Client.Timeout = TimeSpan.FromSeconds(60);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task<long> CreatePartAsync(int stock, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            INSERT INTO parts (workshop_id, code, description, physical_stock)
            VALUES ($1, $2, $3, $4)
            RETURNING id;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(WorkshopId);
        command.Parameters.AddWithValue($"PEC-{Faker.Random.Guid():N}");
        command.Parameters.AddWithValue(Faker.Commerce.ProductName());
        command.Parameters.AddWithValue(stock);

        return (long)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("The integration test part was not inserted."));
    }

    public HttpRequestMessage GetReservationRequest(long partId)
    {
        var input = new CreateReservationRequestDto
        {
            ServiceOrderId = $"OS-{Faker.Random.Guid():N}",
            Items = new() { new() { PartId = partId, Quantity = 1 } }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/reservas")
        {
            Content = JsonContent.Create(input)
        };
        request.Headers.Add("X-User-Id", UserId.ToString(CultureInfo.InvariantCulture));
        request.Headers.Add("Idempotency-Key", Faker.Random.Guid().ToString());
        return request;
    }

    public async Task<long> CountReservationsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM reservations;", connection);

        return (long)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Could not count persisted reservations."));
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();

        try
        {
            if (_factory is not null)
                await _factory.DisposeAsync();
        }
        finally
        {
            await _postgres.DisposeAsync();
        }
    }
}
