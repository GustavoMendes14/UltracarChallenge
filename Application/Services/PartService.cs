using UltracarInventoryApi.Application.Common.Pagination;
using UltracarInventoryApi.Application.Common.Results;
using UltracarInventoryApi.Application.Dtos.Parts;
using UltracarInventoryApi.Application.Interfaces.Services;

namespace UltracarInventoryApi.Services
{
    public class PartService : IPartService
    {
        public Task<Result<PartBalanceResponseDto>> GetBalanceAsync(long workshopId, long partId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result<CursorPage<PartResponseDto>>> SearchAsync(long workshopId, string? searchTerm, string? cursor, int pageSize, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}