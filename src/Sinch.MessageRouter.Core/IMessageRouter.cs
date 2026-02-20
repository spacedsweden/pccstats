namespace Sinch.MessageRouter.Core;

/// <summary>
/// Defines the contract for routing messages across channels.
/// </summary>
public interface IMessageRouter
{
    /// <summary>
    /// Sends a message to the specified recipient via the appropriate channel.
    /// </summary>
    Task<MessageResult> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a message send operation.
/// </summary>
public sealed class MessageResult
{
    public required string MessageId { get; init; }
    public required MessageStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Represents a message to be sent outbound.
/// </summary>
public sealed class OutboundMessage
{
    public required string To { get; init; }
    public required string From { get; init; }
    public required string Body { get; init; }
    public required ChannelType Channel { get; init; }
}

/// <summary>
/// Supported messaging channels.
/// </summary>
public enum ChannelType
{
    Sms,
    Rcs,
    WhatsApp,
    Viber,
    Messenger
}

/// <summary>
/// Status of a sent message.
/// </summary>
public enum MessageStatus
{
    Queued,
    Sent,
    Delivered,
    Failed
}
