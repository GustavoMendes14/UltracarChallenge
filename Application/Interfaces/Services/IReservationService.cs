using UltracarInventoryApi.Application.Common.Results;
using UltracarInventoryApi.Application.Dtos.Reservation;

namespace UltracarInventoryApi.Application.Interfaces.Services;

public interface IReservationService
{
    Task<Result<ReservationResponseDto>> CreateAsync(
        long workshopId,
        long userId,
        string idempotencyKey,
        CreateReservationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<Result<ReservationResponseDto>> ConfirmAsync(
        long workshopId,
        long reservationId,
        CancellationToken cancellationToken = default);

    Task<Result<ReservationResponseDto>> CancelAsync(
        long workshopId,
        long reservationId,
        CancellationToken cancellationToken = default);
}