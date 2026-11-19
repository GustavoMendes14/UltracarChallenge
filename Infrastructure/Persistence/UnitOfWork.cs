using System.Data;
using Npgsql;
using UltracarInventoryApi.Application.Interfaces.Persistence;

namespace UltracarInventoryApi.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    private NpgsqlConnection? _connection;
    private bool _disposed;

    internal NpgsqlTransaction? Transaction { get; private set; }

    public UnitOfWork(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    internal async Task<NpgsqlConnection> GetConnectionAsync(
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _connection ??=
            await _dataSource.OpenConnectionAsync(cancellationToken);
    }

    internal void RequireTransaction()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Transaction is null)
        {
            throw new InvalidOperationException(
                "An active transaction is required.");
        }
    }

    public async Task BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        if (Transaction is not null)
        {
            throw new InvalidOperationException(
                "A transaction is already active.");
        }

        var connection = await GetConnectionAsync(cancellationToken);

        Transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
    }

    public async Task CommitAsync(
        CancellationToken cancellationToken = default)
    {
        RequireTransaction();

        var transaction = Transaction!;

        await transaction.CommitAsync(cancellationToken);

        Transaction = null;

        await transaction.DisposeAsync();
    }

    public async Task RollbackAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Transaction is null)
        {
            return;
        }

        var transaction = Transaction;

        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            Transaction = null;

            await transaction.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (Transaction is not null)
            {
                await Transaction.DisposeAsync();
            }
        }
        finally
        {
            Transaction = null;

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            _connection = null;
        }
    }
}