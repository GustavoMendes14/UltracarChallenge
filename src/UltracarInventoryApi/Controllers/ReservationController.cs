using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UltracarInventoryApi.Application.Dtos.Reservation;
using UltracarInventoryApi.Application.Interfaces.Services;
using UltracarInventoryApi.Utils.Extensions;

namespace UltracarInventoryApi.Controllers;

[ApiController]
[Route("api/v1/reservas")]
public sealed class ReservationController : ControllerBase
{
    private readonly IReservationService _reservationService;

    public ReservationController(
        IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ReservationResponseDto),StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateAsync(
        [FromHeader(Name = "X-Oficina-Id")]
        [Range(1, long.MaxValue)]
        long workshopId,

        [FromHeader(Name = "X-User-Id")]
        [Range(1, long.MaxValue)]
        long userId,

        [FromHeader(Name = "Idempotency-Key")]
        [Required]
        string idempotencyKey,

        [FromBody]
        CreateReservationRequestDto request,

        CancellationToken cancellationToken)
    {
        var result = await _reservationService.CreateAsync(
            workshopId,
            userId,
            idempotencyKey,
            request,
            cancellationToken);

        return result.ToActionResult(
            this,
            StatusCodes.Status201Created);
    }

    [HttpPost("{id:long}/confirmacao")]
    [ProducesResponseType(
        typeof(ReservationResponseDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmAsync(
        [FromRoute]
        [Range(1, long.MaxValue)]
        long id,

        [FromHeader(Name = "X-Oficina-Id")]
        [Range(1, long.MaxValue)]
        long workshopId,

        CancellationToken cancellationToken)
    {
        var result = await _reservationService.ConfirmAsync(
            workshopId,
            id,
            cancellationToken);

        return result.ToActionResult(this);
    }

    [HttpPost("{id:long}/cancelamento")]
    [ProducesResponseType(
        typeof(ReservationResponseDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelAsync(
        [FromRoute]
        [Range(1, long.MaxValue)]
        long id,

        [FromHeader(Name = "X-Oficina-Id")]
        [Range(1, long.MaxValue)]
        long workshopId,

        CancellationToken cancellationToken)
    {
        var result = await _reservationService.CancelAsync(
            workshopId,
            id,
            cancellationToken);

        return result.ToActionResult(this);
    }
}