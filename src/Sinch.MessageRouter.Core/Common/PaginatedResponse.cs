namespace Sinch.MessageRouter.Core.Common;

/// <summary>
/// A paginated response wrapper for list endpoints.
/// </summary>
/// <typeparam name="T">The type of items in the response.</typeparam>
public sealed class PaginatedResponse<T>
{
    /// <summary>
    /// The items in the current page.
    /// </summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// Cursor for the next page, or null if this is the last page.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Total number of items across all pages, if known.
    /// </summary>
    public int? TotalCount { get; init; }

    /// <summary>
    /// The number of items per page.
    /// </summary>
    public int PageSize { get; init; }
}
