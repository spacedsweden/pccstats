namespace Sinch.MessageRouter.Core.Messages;

/// <summary>
/// Paginated response for message listing.
/// </summary>
public sealed class MessageListResponse
{
    /// <summary>List of messages.</summary>
    public required IReadOnlyList<MessageResponse> Data { get; init; }

    /// <summary>Cursor for the next page, null if no more pages.</summary>
    public string? NextCursor { get; init; }

    /// <summary>Whether there are more results.</summary>
    public bool HasMore { get; init; }

    /// <summary>Page size used for this query.</summary>
    public int PageSize { get; init; }
}
