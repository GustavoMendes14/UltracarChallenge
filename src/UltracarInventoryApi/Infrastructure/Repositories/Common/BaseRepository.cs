using Dapper;
using UltracarInventoryApi.Infrastructure.Persistence;

namespace UltracarInventoryApi.Infrastructure.Repositories.Common;

public abstract class BaseRepository
{
    protected UnitOfWork UnitOfWork { get; }

    protected BaseRepository(UnitOfWork unitOfWork)
    {
        UnitOfWork = unitOfWork;
    }

    protected async Task<IReadOnlyList<TResult>> QueryAsync<TResult>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await UnitOfWork.GetConnectionAsync(
            cancellationToken);

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: UnitOfWork.Transaction,
            cancellationToken: cancellationToken);

        var result = await connection.QueryAsync<TResult>(command);

        return result.ToList();
    }

    protected async Task<TResult?> QuerySingleOrDefaultAsync<TResult>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
        where TResult : class
    {
        var connection = await UnitOfWork.GetConnectionAsync(
            cancellationToken);

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: UnitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<TResult>(
            command);
    }

    protected async Task<TResult> QueryScalarAsync<TResult>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await UnitOfWork.GetConnectionAsync(
            cancellationToken);

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: UnitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await connection.QuerySingleAsync<TResult>(command);
    }

    protected async Task<int> ExecuteAsync(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await UnitOfWork.GetConnectionAsync(
            cancellationToken);

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: UnitOfWork.Transaction,
            cancellationToken: cancellationToken);

        return await connection.ExecuteAsync(command);
    }
}