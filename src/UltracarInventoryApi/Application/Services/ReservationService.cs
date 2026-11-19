using System.Security.Cryptography;
using System.Text.Json;
using UltracarInventoryApi.Application.Common.Results;
using UltracarInventoryApi.Application.Dtos.Reservation;
using UltracarInventoryApi.Application.Interfaces.Persistence;
using UltracarInventoryApi.Application.Interfaces.Services;
using UltracarInventoryApi.Domain.Entities;
using UltracarInventoryApi.Domain.Enums;
using UltracarInventoryApi.Domain.Exceptions;
using UltracarInventoryApi.Domain.ValueObjects;

namespace UltracarInventoryApi.Application.Services;

public sealed class ReservationService : IReservationService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPartRepository _parts;
    private readonly IReservationRepository _reservations;
    private readonly IIdempotencyRepository _idempotency;

    public ReservationService(
        IUnitOfWork unitOfWork,
        IPartRepository parts,
        IReservationRepository reservations,
        IIdempotencyRepository idempotency)
    {
        _unitOfWork = unitOfWork;
        _parts = parts;
        _reservations = reservations;
        _idempotency = idempotency;
    }

    public sealed record StoredOutcome(
        ReservationResponseDto? Response,
        Error? Error);

    public async Task<Result<ReservationResponseDto>> CreateAsync(
        long workshopId,
        long userId,
        string idempotencyKey,
        CreateReservationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (workshopId <= 0 || userId <= 0)
        {
            return Result<ReservationResponseDto>.Failure(
                new Error(
                    "INVALID_CONTEXT",
                    "Workshop ID and user ID must be positive.",
                    ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > 200)
        {
            return Result<ReservationResponseDto>.Failure(
                new Error(
                    "INVALID_IDEMPOTENCY_KEY",
                    "Idempotency-Key is required and must contain at most 200 characters.",
                    ErrorType.Validation));
        }

        if (request is null
            || string.IsNullOrWhiteSpace(request.ServiceOrderId)
            || request.ServiceOrderId.Length > 100
            || request.Items is null
            || request.Items.Count == 0
            || request.Items.Any(item =>
                item is null
                || item.PartId <= 0
                || item.Quantity <= 0))
        {
            return Result<ReservationResponseDto>.Failure(
                new Error(
                    "INVALID_RESERVATION",
                    "Provide a service order and items with positive part IDs and quantities.",
                    ErrorType.Validation));
        }

        if (request.Items
            .Select(item => item.PartId)
            .Distinct()
            .Count() != request.Items.Count)
        {
            return Result<ReservationResponseDto>.Failure(
                new Error(
                    "DUPLICATE_PART",
                    "A part can appear only once in a reservation.",
                    ErrorType.Validation));
        }

        var requestHash = Convert.ToHexString(
            SHA256.HashData(
                JsonSerializer.SerializeToUtf8Bytes(
                    request,
                    JsonOptions)));

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var acquired = await _idempotency.TryBeginAsync(
                workshopId,
                idempotencyKey,
                requestHash,
                cancellationToken);

            if (!acquired)
            {
                var stored = await _idempotency.GetAsync(
                    workshopId,
                    idempotencyKey,
                    cancellationToken);

                if (!string.Equals(
                    stored.RequestHash,
                    requestHash,
                    StringComparison.Ordinal))
                {
                    await _unitOfWork.RollbackAsync(
                        CancellationToken.None);

                    return Result<ReservationResponseDto>.Failure(
                        new Error(
                            "IDEMPOTENCY_KEY_REUSED",
                            "The idempotency key was already used with a different request body.",
                            ErrorType.UnprocessableEntity));
                }

                var outcome = JsonSerializer.Deserialize<StoredOutcome>(
                    stored.ResponseJson
                        ?? throw new InvalidOperationException(
                            "Incomplete idempotency record."),
                    JsonOptions)
                    ?? throw new InvalidOperationException(
                        "Invalid idempotency response.");

                var replay = outcome.Error is not null
                    ? Result<ReservationResponseDto>.Failure(outcome.Error)
                    : Result<ReservationResponseDto>.Success(
                        outcome.Response
                            ?? throw new InvalidOperationException(
                                "Missing reservation response."));

                await _unitOfWork.CommitAsync(cancellationToken);

                return replay;
            }

            var partIds = request.Items
                .Select(item => item.PartId)
                .Order()
                .ToArray();

            var lockedParts = await _parts.GetByIdsForUpdateAsync(
                workshopId,
                partIds,
                cancellationToken);

            Result<ReservationResponseDto> result;

            if (lockedParts.Count != partIds.Length)
            {
                result = Result<ReservationResponseDto>.Failure(
                    new Error(
                        "PART_NOT_FOUND",
                        "One or more parts were not found in this workshop.",
                        ErrorType.NotFound));
            }
            else
            {
                var now = await _unitOfWork.GetDatabaseTimeAsync(
                    cancellationToken);

                var activeItems =
                    await _reservations.GetActiveItemsByPartIdsAsync(
                        workshopId,
                        partIds,
                        now,
                        cancellationToken);

                var reservedByPart = activeItems
                    .GroupBy(item => item.PartId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Sum(item => (long)item.Quantity));

                var partsById = lockedParts.ToDictionary(part => part.Id);

                var shortages = request.Items
                    .Select(item => new
                    {
                        partId = item.PartId,
                        requestedQuantity = item.Quantity,
                        availableStock =
                            partsById[item.PartId].PhysicalStock
                            - reservedByPart.GetValueOrDefault(item.PartId)
                    })
                    .Where(item =>
                        item.requestedQuantity > item.availableStock)
                    .ToArray();

                if (shortages.Length > 0)
                {
                    result = Result<ReservationResponseDto>.Failure(
                        new Error(
                            "INSUFFICIENT_STOCK",
                            "Insufficient stock for one or more parts.",
                            ErrorType.Conflict,
                            shortages));
                }
                else
                {
                    var reservation = ReservationEntity.Create(
                        workshopId,
                        request.ServiceOrderId,
                        userId,
                        request.Items.Select(item =>
                            new ReservationLine(
                                item.PartId,
                                item.Quantity)),
                        now);

                    var inserted = await _reservations.AddAsync(
                        reservation,
                        cancellationToken);

                    result = Result<ReservationResponseDto>.Success(
                        ReservationResponseDto.FromEntity(inserted));
                }
            }

            var snapshot = result.IsSuccess
                ? new StoredOutcome(result.Value, null)
                : new StoredOutcome(null, result.Error);

            await _idempotency.CompleteAsync(
                workshopId,
                idempotencyKey,
                JsonSerializer.Serialize(snapshot, JsonOptions),
                cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<Result<ReservationResponseDto>> ConfirmAsync(
        long workshopId,
        long reservationId,
        CancellationToken cancellationToken = default)
    {
        return TransitionAsync(
            workshopId,
            reservationId,
            confirm: true,
            cancellationToken: cancellationToken);
    }

    public Task<Result<ReservationResponseDto>> CancelAsync(
        long workshopId,
        long reservationId,
        CancellationToken cancellationToken = default)
    {
        return TransitionAsync(
            workshopId,
            reservationId,
            confirm: false,
            cancellationToken: cancellationToken);
    }

    private async Task<Result<ReservationResponseDto>> TransitionAsync(
        long workshopId,
        long reservationId,
        bool confirm,
        CancellationToken cancellationToken)
    {
        if (workshopId <= 0 || reservationId <= 0)
        {
            return Result<ReservationResponseDto>.Failure(
                new Error(
                    "INVALID_IDENTIFIER",
                    "Workshop ID and reservation ID must be positive.",
                    ErrorType.Validation));
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var reservation =
                await _reservations.GetByIdForUpdateAsync(
                    workshopId,
                    reservationId,
                    cancellationToken);

            if (reservation is null)
            {
                await _unitOfWork.RollbackAsync(CancellationToken.None);

                return Result<ReservationResponseDto>.Failure(
                    new Error(
                        "RESERVATION_NOT_FOUND",
                        "Reservation not found in this workshop.",
                        ErrorType.NotFound));
            }

            if (reservation.Status != ReservationStatus.Active)
            {
                await _unitOfWork.RollbackAsync(CancellationToken.None);

                return Result<ReservationResponseDto>.Failure(
                    new Error(
                        reservation.Status == ReservationStatus.Expired
                            ? "RESERVATION_EXPIRED"
                            : "INVALID_RESERVATION_TRANSITION",
                        "Only an active, unexpired reservation can be changed.",
                        ErrorType.Conflict));
            }

            IReadOnlyList<PartEntity> lockedParts =
                Array.Empty<PartEntity>();

            if (confirm)
            {
                lockedParts = await _parts.GetByIdsForUpdateAsync(
                    workshopId,
                    reservation.Items
                        .Select(item => item.PartId)
                        .Order()
                        .ToArray(),
                    cancellationToken);

                if (lockedParts.Count != reservation.Items.Count)
                {
                    await _unitOfWork.RollbackAsync(
                        CancellationToken.None);

                    return Result<ReservationResponseDto>.Failure(
                        new Error(
                            "PART_NOT_FOUND",
                            "A reservation part was not found in this workshop.",
                            ErrorType.NotFound));
                }
            }

            var now = await _unitOfWork.GetDatabaseTimeAsync(
                cancellationToken);

            if (reservation.TryExpire(now))
            {
                await _reservations.UpdateAsync(
                    reservation,
                    cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                return Result<ReservationResponseDto>.Failure(
                    new Error(
                        "RESERVATION_EXPIRED",
                        "The reservation has expired and cannot be changed.",
                        ErrorType.Conflict));
            }

            if (confirm)
            {
                var partsById = lockedParts.ToDictionary(part => part.Id);

                foreach (var item in reservation.Items)
                {
                    partsById[item.PartId]
                        .EnsurePhysicalStockAvailable(item.Quantity);
                }

                reservation.Confirm(now);

                foreach (var item in reservation.Items.OrderBy(
                    item => item.PartId))
                {
                    var part = partsById[item.PartId];

                    part.DecreasePhysicalStock(item.Quantity);

                    await _parts.UpdateAsync(
                        part,
                        cancellationToken);
                }
            }
            else
            {
                reservation.Cancel(now);
            }

            await _reservations.UpdateAsync(
                reservation,
                cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            return Result<ReservationResponseDto>.Success(
                ReservationResponseDto.FromEntity(reservation));
        }
        catch (DomainException exception)
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);

            return Result<ReservationResponseDto>.Failure(
                new Error(
                    exception.Code.ToUpperInvariant(),
                    exception.Message,
                    ErrorType.Conflict));
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
