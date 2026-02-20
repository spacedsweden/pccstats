namespace Sinch.MessageRouter.Core.Messages;

/// <summary>
/// Response returned after sending a message or retrieving message details.
/// </summary>
public sealed class MessageResponse
{
    /// <summary>Unique message identifier.</summary>
    public required string MessageId { get; init; }

    /// <summary>The channel the message was sent through.</summary>
    public MessageChannel Channel { get; init; }

    /// <summary>The recipient identifier.</summary>
    public required string To { get; init; }

    /// <summary>The sender identifier.</summary>
    public string? From { get; init; }

    /// <summary>Current delivery status.</summary>
    public MessageStatus Status { get; init; }

    /// <summary>Timestamp when the message was accepted by the gateway.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Timestamp of the last status update.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>The message content.</summary>
    public MessageContent? Content { get; init; }

    /// <summary>Error details if the message failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Metadata attached to the message.</summary>
    public Dictionary<string, string>? Metadata { get; init; }

    /// <summary>Correlation ID for tracing.</summary>
    public string? CorrelationId { get; init; }
}
