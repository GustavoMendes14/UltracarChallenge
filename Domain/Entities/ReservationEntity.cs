using System;
using System.Collections.Generic;
using System.Linq;
using UltracarInventoryApi.Domain.Enums;
using UltracarInventoryApi.Domain.Exceptions;
using UltracarInventoryApi.Domain.ValueObjects;

namespace UltracarInventoryApi.Domain.Entities;

public sealed class ReservationEntity
{
    public static TimeSpan Lifetime { get; } =
        TimeSpan.FromMinutes(30);

    private readonly List<ReservationItem> _items = new();

    public long Id { get; private set; }
    public long WorkshopId { get; private set; }
    public long ServiceOrderId { get; private set; }
    public long UserId { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public IReadOnlyCollection<ReservationItem> Items =>
        _items.AsReadOnly();

    private ReservationEntity()
    {
    }

    internal ReservationEntity(
    long id,
    long workshopId,
    long serviceOrderId,
    long userId,
    ReservationStatus status,
    DateTimeOffset createdAt,
    DateTimeOffset expiresAt,
    IEnumerable<ReservationItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (id <= 0
            || workshopId <= 0
            || serviceOrderId <= 0
            || userId <= 0
            || !Enum.IsDefined(status))
        {
            throw new InvalidOperationException(
                "The persisted reservation data is invalid.");
        }

        if (expiresAt != createdAt.Add(Lifetime))
        {
            throw new InvalidOperationException(
                "The persisted reservation expiration is invalid.");
        }

        var snapshot = items.ToArray();

        if (snapshot.Length == 0)
        {
            throw new InvalidOperationException(
                "The persisted reservation must contain at least one item.");
        }

        if (snapshot.Any(item =>
            item is null
            || item.Id <= 0
            || item.ReservationId != id
            || item.WorkshopId != workshopId
            || item.PartId <= 0
            || item.Quantity <= 0))
        {
            throw new InvalidOperationException(
                "The persisted reservation items are invalid.");
        }

        if (snapshot
            .Select(item => item.PartId)
            .Distinct()
            .Count() != snapshot.Length)
        {
            throw new InvalidOperationException(
                "The persisted reservation contains duplicate parts.");
        }

        Id = id;
        WorkshopId = workshopId;
        ServiceOrderId = serviceOrderId;
        UserId = userId;
        Status = status;
        CreatedAt = createdAt.ToUniversalTime();
        ExpiresAt = expiresAt.ToUniversalTime();

        _items.AddRange(
            snapshot.OrderBy(item => item.PartId));
    }

    public static ReservationEntity Create(
        long workshopId,
        long serviceOrderId,
        long userId,
        IEnumerable<ReservationLine> lines,
        DateTimeOffset now)
    {
        if (workshopId <= 0)
        {
            throw new DomainException(
                "invalid_workshop_id",
                "Workshop ID must be positive.");
        }

        if (serviceOrderId <= 0)
        {
            throw new DomainException(
                "invalid_service_order_id",
                "Service order ID must be positive.");
        }

        if (userId <= 0)
        {
            throw new DomainException(
                "invalid_user_id",
                "User ID must be positive.");
        }

        ArgumentNullException.ThrowIfNull(lines);

        var snapshot = lines.ToArray();

        if (snapshot.Length == 0)
        {
            throw new DomainException(
                "empty_reservation",
                "A reservation must contain at least one item.");
        }

        if (snapshot.Any(line => line is null))
        {
            throw new DomainException(
                "invalid_reservation_item",
                "Reservation items cannot be null.");
        }

        var distinctPartCount = snapshot
            .Select(line => line.PartId)
            .Distinct()
            .Count();

        if (distinctPartCount != snapshot.Length)
        {
            throw new DomainException(
                "duplicate_part",
                "A part can appear only once in a reservation.");
        }

        var createdAt = now.ToUniversalTime();

        var reservation = new ReservationEntity
        {
            WorkshopId = workshopId,
            ServiceOrderId = serviceOrderId,
            UserId = userId,
            Status = ReservationStatus.Active,
            CreatedAt = createdAt,
            ExpiresAt = createdAt.Add(Lifetime)
        };

        foreach (var line in snapshot.OrderBy(line => line.PartId))
        {
            reservation._items.Add(
                new ReservationItem(workshopId, line));
        }

        return reservation;
    }

    public bool IsActiveAt(DateTimeOffset now)
    {
        return Status == ReservationStatus.Active
            && now >= CreatedAt
            && now < ExpiresAt;
    }

    public bool Confirm(DateTimeOffset now)
    {
        EnsureValidOperationTime(now);

        if (Status == ReservationStatus.Confirmed)
        {
            return false;
        }

        EnsureActiveAndUnexpired(now);

        Status = ReservationStatus.Confirmed;

        return true;
    }

    public bool Cancel(DateTimeOffset now)
    {
        EnsureValidOperationTime(now);

        if (Status == ReservationStatus.Cancelled)
        {
            return false;
        }

        EnsureActiveAndUnexpired(now);

        Status = ReservationStatus.Cancelled;

        return true;
    }

    public bool TryExpire(DateTimeOffset now)
    {
        EnsureValidOperationTime(now);

        if (Status != ReservationStatus.Active || now < ExpiresAt)
        {
            return false;
        }

        Status = ReservationStatus.Expired;

        return true;
    }

    private void EnsureActiveAndUnexpired(DateTimeOffset now)
    {
        if (Status == ReservationStatus.Expired)
        {
            throw new DomainException(
                "reservation_expired",
                "The reservation has expired.");
        }

        if (Status != ReservationStatus.Active)
        {
            throw new DomainException(
                "invalid_reservation_transition",
                "The reservation is no longer active.");
        }

        if (now >= ExpiresAt)
        {
            throw new DomainException(
                "reservation_expired",
                "The reservation has expired.");
        }
    }

    private void EnsureValidOperationTime(DateTimeOffset now)
    {
        if (now < CreatedAt)
        {
            throw new DomainException(
                "invalid_operation_time",
                "Operation time cannot precede reservation creation.");
        }
    }
}