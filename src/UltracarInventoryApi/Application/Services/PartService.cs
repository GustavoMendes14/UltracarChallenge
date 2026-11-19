using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using UltracarInventoryApi.Application.Common.Pagination;
using UltracarInventoryApi.Application.Common.Results;
using UltracarInventoryApi.Application.Dtos.Parts;
using UltracarInventoryApi.Application.Interfaces.Persistence;
using UltracarInventoryApi.Application.Interfaces.Services;
using UltracarInventoryApi.Domain.Enums;

namespace UltracarInventoryApi.Application.Services;

public sealed class PartService : IPartService
{
    private readonly IPartRepository _parts;

    public PartService(IPartRepository parts)
    {
        _parts = parts;
    }

    private sealed record CursorPayload(
        long WorkshopId,
        string SearchTerm,
        long AfterId);

    public async Task<Result<PartBalanceResponseDto>> GetBalanceAsync(
        long workshopId,
        long partId,
        CancellationToken cancellationToken = default)
    {
        if (workshopId <= 0 || partId <= 0)
        {
            return Result<PartBalanceResponseDto>.Failure(
                new Error(
                    "INVALID_IDENTIFIER",
                    "Workshop ID and part ID must be positive.",
                    ErrorType.Validation));
        }

        var balance = await _parts.GetBalanceAsync(
            workshopId,
            partId,
            cancellationToken);

        return balance is null
            ? Result<PartBalanceResponseDto>.Failure(
                new Error(
                    "PART_NOT_FOUND",
                    "Part not found in this workshop.",
                    ErrorType.NotFound))
            : Result<PartBalanceResponseDto>.Success(balance);
    }

    public async Task<Result<CursorPage<PartResponseDto>>> SearchAsync(
        long workshopId,
        string? searchTerm,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (workshopId <= 0 || pageSize is < 1 or > 20)
        {
            return Result<CursorPage<PartResponseDto>>.Failure(
                new Error(
                    "INVALID_QUERY",
                    "Workshop ID must be positive and page size must be between 1 and 20.",
                    ErrorType.Validation));
        }

        var term = searchTerm?.Trim() ?? string.Empty;

        if (term.Length > 200)
        {
            return Result<CursorPage<PartResponseDto>>.Failure(
                new Error(
                    "INVALID_QUERY",
                    "Search term must contain at most 200 characters.",
                    ErrorType.Validation));
        }

        long? afterId = null;

        if (cursor is not null)
        {
            if (cursor.Length is 0 or > 4096)
            {
                return Result<CursorPage<PartResponseDto>>.Failure(
                    new Error(
                        "INVALID_QUERY",
                        "Invalid cursor.",
                        ErrorType.Validation));
            }

            try
            {
                var payload = JsonSerializer.Deserialize<CursorPayload>(
                    WebEncoders.Base64UrlDecode(cursor));

                if (payload is null
                    || payload.WorkshopId != workshopId
                    || payload.SearchTerm != term
                    || payload.AfterId <= 0)
                {
                    return Result<CursorPage<PartResponseDto>>.Failure(
                        new Error(
                            "INVALID_QUERY",
                            "The cursor does not match the workshop and search term.",
                            ErrorType.Validation));
                }

                afterId = payload.AfterId;
            }
            catch (Exception exception) when (
                exception is FormatException
                    or JsonException
                    or ArgumentException)
            {
                return Result<CursorPage<PartResponseDto>>.Failure(
                    new Error(
                        "INVALID_QUERY",
                        "Invalid cursor.",
                        ErrorType.Validation));
            }
        }

        var found = await _parts.SearchAsync(
            workshopId,
            term,
            afterId,
            pageSize + 1,
            cancellationToken);

        var items = found.Take(pageSize).ToArray();

        var nextCursor = found.Count > pageSize
            ? WebEncoders.Base64UrlEncode(
                JsonSerializer.SerializeToUtf8Bytes(
                    new CursorPayload(
                        workshopId,
                        term,
                        items[^1].Id)))
            : null;

        return Result<CursorPage<PartResponseDto>>.Success(
            new CursorPage<PartResponseDto>(
                items,
                nextCursor));
    }
}
