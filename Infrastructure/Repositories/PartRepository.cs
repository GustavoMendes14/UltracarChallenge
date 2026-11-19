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

    public Task<PartEntity?> GetByIdAsync(
        long workshopId,
        long partId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id AS "Id",
                workshop_id AS "WorkshopId",
                code AS "Code",
                description AS "Description",
                physical_stock AS "PhysicalStock"
            FROM parts
            WHERE workshop_id = @WorkshopId
              AND id = @PartId;
            """;

        return QuerySingleOrDefaultAsync<PartEntity>(
            sql,
            new
            {
                WorkshopId = workshopId,
                PartId = partId
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

    public Task<IReadOnlyList<PartEntity>> SearchAsync(
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

        if (afterPartId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(afterPartId));
        }

        const string sql = """
            SELECT
                id AS "Id",
                workshop_id AS "WorkshopId",
                code AS "Code",
                description AS "Description",
                physical_stock AS "PhysicalStock"
            FROM parts
            WHERE workshop_id = @WorkshopId
              AND id > COALESCE(@AfterPartId, 0)
              AND (
                  @Pattern IS NULL
                  OR code ILIKE @Pattern ESCAPE '\'
                  OR description ILIKE @Pattern ESCAPE '\'
              )
            ORDER BY id
            LIMIT @Limit;
            """;

        return QueryAsync<PartEntity>(
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
                "The part was not found in the workshop.");
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