using UltracarInventoryApi.Application.Common.Pagination;
using UltracarInventoryApi.Application.Common.Results;
using UltracarInventoryApi.Application.Dtos.Parts;

namespace UltracarInventoryApi.Application.Interfaces.Services;

public interface IPartService
{
    Task<Result<PartBalanceResponseDto>> GetBalanceAsync(
        long workshopId,
        long partId,
        CancellationToken cancellationToken = default);

    Task<Result<CursorPage<PartResponseDto>>> SearchAsync(
        long workshopId,
        string? searchTerm,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken = default);
}