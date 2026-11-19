namespace UltracarInventoryApi.Application.Interfaces.Persistence;

public interface IUnitOfWork
{
    Task<DateTimeOffset> GetDatabaseTimeAsync(
        CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(
        CancellationToken cancellationToken = default);

    Task CommitAsync(
        CancellationToken cancellationToken = default);

    Task RollbackAsync(
        CancellationToken cancellationToken = default);
}