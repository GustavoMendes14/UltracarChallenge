using UltracarInventoryApi.Application.Dtos.Parts;
using UltracarInventoryApi.Domain.Entities;

namespace UltracarInventoryApi.Application.Interfaces.Persistence;

public interface IPartRepository
{
    Task<PartBalanceResponseDto?> GetBalanceAsync(
        long workshopId,
        long partId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartResponseDto>> SearchAsync(
        long workshopId,
        string? searchTerm,
        long? afterPartId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartEntity>> GetByIdsForUpdateAsync(
        long workshopId,
        IReadOnlyCollection<long> partIds,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        PartEntity part,
        CancellationToken cancellationToken = default);
}