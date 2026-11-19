using UltracarInventoryApi.Application.Dtos.Parts;
using UltracarInventoryApi.Application.Interfaces.Persistence;
using UltracarInventoryApi.Domain.Entities;
using UltracarInventoryApi.Infrastructure.Persistence;
using UltracarInventoryApi.Infrastructure.Repositories.Common;

namespace UltracarInventoryApi.Infrastructure.Repositories;

public sealed class PartRepository : BaseRepository, IPartRepository
{
    public PartRepository(UnitOfWork unitOfWork)
        : base(unitOfWork)
    {
    }

    public Task<PartBalanceResponseDto?> GetBalanceAsync(
        long workshopId,
        long partId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH reference_time AS MATERIALIZED (
                SELECT clock_timestamp() AS now
            )
            SELECT
                p.id AS "PartId",
                p.physical_stock AS "PhysicalStock",
                s.reserved_stock AS "ReservedStock",
                p.physical_stock - s.reserved_stock AS "AvailableStock"
            FROM parts p
            CROSS JOIN reference_time t
            CROSS JOIN LATERAL (
                SELECT
                    COALESCE(
                        SUM(ri.quantity::bigint),
                        0
                    )::bigint AS reserved_stock
                FROM reservation_items ri
                JOIN reservations r
                  ON r.id = ri.reservation_id
                 AND r.workshop_id = ri.workshop_id
                WHERE ri.workshop_id = p.workshop_id
                  AND ri.part_id = p.id
                  AND r.status = 1
                  AND r.expires_at > t.now
            ) s
            WHERE p.workshop_id = @WorkshopId
              AND p.id = @PartId;
            """;

        return QuerySingleOrDefaultAsync<PartBalanceResponseDto>(
            sql,
            new
            {
                WorkshopId = workshopId,
                PartId = partId
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<PartResponseDto>> SearchAsync(
        long workshopId,
        string? searchTerm,
        long? afterPartId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 21)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        const string sql = """
            WITH reference_time AS MATERIALIZED (
                SELECT clock_timestamp() AS now
            ),
            page AS MATERIALIZED (
                SELECT
                    id,
                    workshop_id,
                    code,
                    description,
                    physical_stock
                FROM parts
                WHERE workshop_id = @WorkshopId
                  AND id > COALESCE(@AfterPartId::bigint, 0)
                  AND (
                      @Pattern::text IS NULL
                      OR code ILIKE @Pattern ESCAPE '\'
                      OR description ILIKE @Pattern ESCAPE '\'
                  )
                ORDER BY id
                LIMIT @Limit
            )
            SELECT
                p.id AS "Id",
                p.code AS "Code",
                p.description AS "Description",
                p.physical_stock AS "PhysicalStock",
                s.reserved_stock AS "ReservedStock",
                p.physical_stock - s.reserved_stock AS "AvailableStock"
            FROM page p
            CROSS JOIN reference_time t
            CROSS JOIN LATERAL (
                SELECT
                    COALESCE(
                        SUM(ri.quantity::bigint),
                        0
                    )::bigint AS reserved_stock
                FROM reservation_items ri
                JOIN reservations r
                  ON r.id = ri.reservation_id
                 AND r.workshop_id = ri.workshop_id
                WHERE ri.workshop_id = p.workshop_id
                  AND ri.part_id = p.id
                  AND r.status = 1
                  AND r.expires_at > t.now
            ) s
            ORDER BY p.id;
            """;

        return QueryAsync<PartResponseDto>(
            sql,
            new
            {
                WorkshopId = workshopId,
                AfterPartId = afterPartId,
                Pattern = BuildSearchPattern(searchTerm),
                Limit = limit
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<PartEntity>> GetByIdsForUpdateAsync(
        long workshopId,
        IReadOnlyCollection<long> partIds,
        CancellationToken cancellationToken = default)
    {
        UnitOfWork.RequireTransaction();

        const string sql = """
            SELECT
                id AS "Id",
                workshop_id AS "WorkshopId",
                code AS "Code",
                description AS "Description",
                physical_stock AS "PhysicalStock"
            FROM parts
            WHERE workshop_id = @WorkshopId
              AND id = ANY(@PartIds)
            ORDER BY id
            FOR UPDATE;
            """;

        return QueryAsync<PartEntity>(
            sql,
            new
            {
                WorkshopId = workshopId,
                PartIds = partIds
                    .Distinct()
                    .Order()
                    .ToArray()
            },
            cancellationToken);
    }

    public async Task UpdateAsync(
        PartEntity part,
        CancellationToken cancellationToken = default)
    {
        UnitOfWork.RequireTransaction();

        const string sql = """
            UPDATE parts
            SET physical_stock = @PhysicalStock
            WHERE workshop_id = @WorkshopId
              AND id = @Id;
            """;

        var affected = await ExecuteAsync(
            sql,
            new
            {
                part.Id,
                part.WorkshopId,
                part.PhysicalStock
            },
            cancellationToken);

        if (affected != 1)
        {
            throw new InvalidOperationException(
                "Part not found in workshop.");
        }
    }

    private static string? BuildSearchPattern(string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        var escaped = searchTerm.Trim()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        return $"%{escaped}%";
    }
}