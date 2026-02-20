namespace Sinch.MessageRouter.Core.Messages;

/// <summary>
/// Query parameters for listing messages.
/// </summary>
public sealed class MessageListQuery
{
    public MessageStatus? Status { get; init; }
    public MessageChannel? Channel { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public DateTimeOffset? After { get; init; }
    public DateTimeOffset? Before { get; init; }
    public string? Cursor { get; init; }
    public int PageSize { get; init; } = 20;
}
