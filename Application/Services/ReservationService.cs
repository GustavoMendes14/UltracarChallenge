using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UltracarInventoryApi.Services.Interfaces;

namespace UltracarInventoryApi.Services
{
    public class ReservationService : IReservationService
    {
        public Task<ReservationResponseDto?> CancelAsync(long workshopId, long reservationId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ReservationResponseDto?> ConfirmAsync(long workshopId, long reservationId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ReservationResponseDto> CreateAsync(long workshopId, long userId, string idempotencyKey, CreateReservationRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}