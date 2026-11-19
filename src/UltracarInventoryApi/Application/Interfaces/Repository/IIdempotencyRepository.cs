using UltracarInventoryApi.Application.Common.Results;

namespace UltracarInventoryApi.Application.Interfaces.Persistence;

public interface IIdempotencyRepository
{
    Task<bool> TryBeginAsync(
        long workshopId,
        string key,
        string requestHash,
        CancellationToken cancellationToken = default);

    Task<IdempotencyRecord> GetAsync(
        long workshopId,
        string key,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        long workshopId,
        string key,
        string responseJson,
        CancellationToken cancellationToken = default);
}