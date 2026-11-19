using UltracarInventoryApi.Application.Common.Results;
using UltracarInventoryApi.Application.Interfaces.Persistence;
using UltracarInventoryApi.Infrastructure.Persistence;
using UltracarInventoryApi.Infrastructure.Repositories.Common;

namespace UltracarInventoryApi.Infrastructure.Repositories;

public sealed class IdempotencyRepository
    : BaseRepository, IIdempotencyRepository
{
    public IdempotencyRepository(UnitOfWork unitOfWork)
        : base(unitOfWork)
    {
    }

    public async Task<bool> TryBeginAsync(
        long workshopId,
        string key,
        string requestHash,
        CancellationToken cancellationToken = default)
    {
        UnitOfWork.RequireTransaction();

        const string sql = """
            INSERT INTO reservation_idempotency (
                workshop_id,
                idempotency_key,
                request_hash
            )
            VALUES (
                @WorkshopId,
                @Key,
                @RequestHash
            )
            ON CONFLICT (workshop_id, idempotency_key)
            DO NOTHING;
            """;

        var affected = await ExecuteAsync(
            sql,
            new
            {
                WorkshopId = workshopId,
                Key = key,
                RequestHash = requestHash
            },
            cancellationToken);

        return affected == 1;
    }

    public async Task<IdempotencyRecord> GetAsync(
        long workshopId,
        string key,
        CancellationToken cancellationToken = default)
    {
        UnitOfWork.RequireTransaction();

        const string sql = """
            SELECT
                request_hash AS "RequestHash",
                response_json AS "ResponseJson"
            FROM reservation_idempotency
            WHERE workshop_id = @WorkshopId
              AND idempotency_key = @Key;
            """;

        return await QuerySingleOrDefaultAsync<IdempotencyRecord>(
            sql,
            new
            {
                WorkshopId = workshopId,
                Key = key
            },
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Idempotency record not found.");
    }

    public async Task CompleteAsync(
        long workshopId,
        string key,
        string responseJson,
        CancellationToken cancellationToken = default)
    {
        UnitOfWork.RequireTransaction();

        const string sql = """
            UPDATE reservation_idempotency
            SET response_json = @ResponseJson
            WHERE workshop_id = @WorkshopId
              AND idempotency_key = @Key
              AND response_json IS NULL;
            """;

        var affected = await ExecuteAsync(
            sql,
            new
            {
                WorkshopId = workshopId,
                Key = key,
                ResponseJson = responseJson
            },
            cancellationToken);

        if (affected != 1)
        {
            throw new InvalidOperationException(
                "Idempotency response was not saved.");
        }
    }
}