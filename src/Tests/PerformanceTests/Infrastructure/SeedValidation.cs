using Npgsql;
using UltracarInventoryApi.PerformanceTests.Configuration;

namespace UltracarInventoryApi.PerformanceTests.Infrastructure;

internal static class SeedValidation
{
    public static async Task ValidateAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        const string sql = """
            SELECT
                (SELECT count(*) FROM parts),
                (SELECT count(*) FROM (
                    SELECT workshop_id FROM parts
                    GROUP BY workshop_id
                    HAVING workshop_id BETWEEN 1 AND 50 AND count(*) = 4000
                ) w),
                (SELECT count(*) FROM reservation_items ri
                    JOIN reservations r ON r.id = ri.reservation_id
                        AND r.workshop_id = ri.workshop_id
                    WHERE r.status IN (2, 3, 4) AND r.expires_at <= CURRENT_TIMESTAMP),
                (SELECT count(*) FROM reservation_items);
            """;
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 120 };
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        if (reader.GetInt64(0) != PerformanceSettings.Workshops * PerformanceSettings.PartsPerWorkshop
            || reader.GetInt64(1) != PerformanceSettings.Workshops
            || reader.GetInt64(2) != 2_000_000 || reader.GetInt64(3) != 2_000_000)
        {
            throw new InvalidOperationException(
                "Seed D1 inválido: esperadas 200.000 peças (4.000 por oficina de 1 a 50) e 2.000.000 de itens históricos. Execute Seed/performance-seed.sql em um banco de desempenho vazio.");
        }
    }
}
