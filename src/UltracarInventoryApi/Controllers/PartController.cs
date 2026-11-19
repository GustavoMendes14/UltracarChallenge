using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using UltracarInventoryApi.Application.Common.Pagination;
using UltracarInventoryApi.Application.Dtos.Parts;
using UltracarInventoryApi.Application.Interfaces.Services;
using UltracarInventoryApi.Utils.Extensions;

namespace UltracarInventoryApi.Controllers;

[ApiController]
[Route("api/v1/pecas")]
public sealed class PartController : ControllerBase
{
    private const int DefaultPageSize = 20;

    private readonly IPartService _partService;

    public PartController(IPartService partService)
    {
        _partService = partService;
    }

    [HttpGet("{id:long}/saldo")]
    [ProducesResponseType(typeof(PartBalanceResponseDto),StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalanceAsync(
        [FromRoute]
        [Range(1, long.MaxValue)]
        long id,

        [FromHeader(Name = "X-Oficina-Id")]
        [Range(1, long.MaxValue)]
        long workshopId,

        CancellationToken cancellationToken)
    {
        var result = await _partService.GetBalanceAsync(
            workshopId,
            id,
            cancellationToken);

        return result.ToActionResult(this);
    }

    [HttpGet]
    [ProducesResponseType(
    typeof(CursorPage<PartResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchAsync(
        [FromHeader(Name = "X-Oficina-Id")]
        [Range(1, long.MaxValue)]
        long workshopId,

        [FromQuery(Name = "busca")]
        string? searchTerm,

        [FromQuery]
        string? cursor,

        CancellationToken cancellationToken)
    {
        var result = await _partService.SearchAsync(
            workshopId,
            searchTerm,
            cursor,
            DefaultPageSize,
            cancellationToken);

        return result.ToActionResult(this);
    }
}