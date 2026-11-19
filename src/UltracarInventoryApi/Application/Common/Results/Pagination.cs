namespace UltracarInventoryApi.Application.Common.Pagination;

public sealed record CursorPage<T>(
    IReadOnlyList<T> Items,
    string? NextCursor);