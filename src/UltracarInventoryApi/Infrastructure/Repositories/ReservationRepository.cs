using UltracarInventoryApi.Application.Interfaces.Persistence;
using UltracarInventoryApi.Domain.Entities;
using UltracarInventoryApi.Domain.Enums;
using UltracarInventoryApi.Infrastructure.Persistence;
using UltracarInventoryApi.Infrastructure.Repositories.Common;

namespace UltracarInventoryApi.Infrastructure.Repositories;

public sealed class ReservationRepository
    : BaseRepository, IReservationRepository
{
    public ReservationRepository(UnitOfWork unitOfWork)
        : base(unitOfWork)
    {
    }

    public async Task<ReservationEntity?> GetByIdForUpdateAsync(
        long workshopId,
        long reservationId,
        CancellationToken cancellationToken = default)
    {
        UnitOfWork.RequireTransaction();

        const string sql = """
            SELECT
                id AS "Id",
                workshop_id AS "WorkshopId",
                service_order_id AS "ServiceOrderId",
                user_id AS "UserId",
                status AS "Status",
                created_at AS "CreatedAt",
                expires_at AS "ExpiresAt"
            FROM reservations
            WHERE workshop_id = @WorkshopId
              AND id = @ReservationId
            FOR UPDATE;
            """;

        var parameters = new
        {
            WorkshopId = workshopId,
            ReservationId = reservationId
        };

        var row = await QuerySingleOrDefaultAsync<ReservationRow>(
            sql,
            parameters,
            cancellationToken);

        if (row is null)
        {
            return null;
        }

        const string itemsSql = """
            SELECT
                id AS "Id",
                reservation_id AS "ReservationId",
                workshop_id AS "WorkshopId",
                part_id AS "PartId",
                quantity AS "Quantity"
            FROM reservation_items
            WHERE workshop_id = @WorkshopId
              AND reservation_id = @ReservationId
            ORDER BY part_id;
            """;

        var items = await QueryAsync<ReservationItem>(
            itemsSql,
            parameters,
            cancellationToken);

        return new ReservationEntity(
            row.Id,
            row.WorkshopId,
            row.ServiceOrderId,
            row.UserId,
            (ReservationStatus)row.Status,
            new DateTimeOffset(row.CreatedAt),
            new DateTimeOffset(row.ExpiresAt),
            items);
    }

    public async Task<ReservationEntity> AddAsync(
        ReservationEntity reservation,
        CancellationToken cancellationToken = default)
    {
        UnitOfWork.RequireTransaction();

        if (reservation.Id != 0
            || reservation.Status != ReservationStatus.Active)
        {
            throw new InvalidOperationException(
                "Only a new active reservation can be inserted.");
        }

        const string sql = """
            INSERT INTO reservations (
                workshop_id,
                service_order_id,
                user_id,
                status,
                created_at,
                expires_at
            )
            VALUES (
                @WorkshopId,
                @ServiceOrderId,
                @UserId,
                @Status,
                @CreatedAt,
                @ExpiresAt
            )
            RETURNING id;
            """;

        var reservationId = await QueryScalarAsync<long>(
            sql,
            new
            {
                reservation.WorkshopId,
                reservation.ServiceOrderId,
                reservation.UserId,
                Status = (int)reservation.Status,
                reservation.CreatedAt,
                reservation.ExpiresAt
            },
            cancellationToken);

        const string itemsSql = """
            INSERT INTO reservation_items (
                reservation_id,
                workshop_id,
                part_id,
                quantity
            )
            SELECT
                @ReservationId,
                @WorkshopId,
                item.part_id,
                item.quantity
            FROM unnest(
                @PartIds::bigint[],
                @Quantities::integer[]
            ) AS item(part_id, quantity);
            """;

        var items = reservation.Items
            .OrderBy(item => item.PartId)
            .ToArray();

        await ExecuteAsync(
            itemsSql,
            new
            {
                ReservationId = reservationId,
                reservation.WorkshopId,
                PartIds = items
                    .Select(item => item.PartId)
                    .ToArray(),
                Quantities = items
                    .Select(item => item.Quantity)
                    .ToArray()
            },
            cancellationToken);

        return await GetByIdForUpdateAsync(
            reservation.WorkshopId,
            reservationId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The inserted reservation could not be loaded.");
    }

    public async Task UpdateAsync(
        ReservationEntity reservation,
        CancellationToken cancellationToken = default)
    {
        UnitOfWork.RequireTransaction();

        const string sql = """
            UPDATE reservations
            SET status = @Status
            WHERE workshop_id = @WorkshopId
              AND id = @Id;
            """;

        var affected = await ExecuteAsync(
            sql,
            new
            {
                reservation.Id,
                reservation.WorkshopId,
                Status = (int)reservation.Status
            },
            cancellationToken);

        if (affected != 1)
        {
            throw new InvalidOperationException(
                "The reservation was not found in the workshop.");
        }
    }

    public Task<IReadOnlyList<ReservationItem>> GetActiveItemsByPartIdsAsync(
        long workshopId,
        IReadOnlyCollection<long> partIds,
        DateTimeOffset referenceTime,
        CancellationToken cancellationToken = default)
    {
        UnitOfWork.RequireTransaction();

        const string sql = """
            SELECT
                ri.id AS "Id",
                ri.reservation_id AS "ReservationId",
                ri.workshop_id AS "WorkshopId",
                ri.part_id AS "PartId",
                ri.quantity AS "Quantity"
            FROM reservation_items ri
            JOIN reservations r
              ON r.id = ri.reservation_id
             AND r.workshop_id = ri.workshop_id
            WHERE ri.workshop_id = @WorkshopId
              AND ri.part_id = ANY(@PartIds)
              AND r.status = @ActiveStatus
              AND r.expires_at > @ReferenceTime
            ORDER BY ri.part_id, ri.id;
            """;

        return QueryAsync<ReservationItem>(
            sql,
            new
            {
                WorkshopId = workshopId,
                PartIds = partIds.Distinct().ToArray(),
                ActiveStatus = (int)ReservationStatus.Active,
                ReferenceTime = referenceTime.ToUniversalTime()
            },
            cancellationToken);
    }

    private sealed class ReservationRow
    {
        public long Id { get; set; }
        public long WorkshopId { get; set; }
        public string ServiceOrderId { get; set; } = null!;
        public long UserId { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}