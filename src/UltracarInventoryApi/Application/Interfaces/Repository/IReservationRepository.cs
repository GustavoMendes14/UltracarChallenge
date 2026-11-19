using UltracarInventoryApi.Domain.Entities;

namespace UltracarInventoryApi.Application.Interfaces.Persistence;

public interface IReservationRepository
{
    Task<ReservationEntity?> GetByIdForUpdateAsync(
        long workshopId,
        long reservationId,
        CancellationToken cancellationToken = default);

    Task<ReservationEntity> AddAsync(
        ReservationEntity reservation,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        ReservationEntity reservation,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReservationItem>> GetActiveItemsByPartIdsAsync(
        long workshopId,
        IReadOnlyCollection<long> partIds,
        DateTimeOffset referenceTime,
        CancellationToken cancellationToken = default);
}