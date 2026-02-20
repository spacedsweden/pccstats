namespace Sinch.MessageRouter.Core.Messages;

/// <summary>
/// Request to send a batch of messages.
/// Supports two patterns:
/// 1. A list of individual message requests (Messages property)
/// 2. A single message template sent to multiple recipients (To + shared fields)
/// </summary>
public sealed class BatchSendRequest
{
    /// <summary>
    /// Pattern 1: Individual message requests, each with its own To/Channel/Content.
    /// </summary>
    public IReadOnlyList<SendMessageRequest>? Messages { get; init; }

    /// <summary>
    /// Pattern 2: List of recipients for a shared message template.
    /// </summary>
    public IReadOnlyList<string>? To { get; init; }

    /// <summary>Sender ID for Pattern 2 (shared across all recipients).</summary>
    public string? From { get; init; }

    /// <summary>Text body for Pattern 2 (shared across all recipients).</summary>
    public string? Body { get; init; }

    /// <summary>Channel for Pattern 2 (shared across all recipients).</summary>
    public MessageChannel? Channel { get; init; }

    /// <summary>Rich content for Pattern 2 (shared across all recipients).</summary>
    public MessageContent? Content { get; init; }

    /// <summary>Priority for Pattern 2 (shared across all recipients).</summary>
    public Priority Priority { get; init; } = Priority.Normal;

    /// <summary>Callback URL for Pattern 2 (shared across all recipients).</summary>
    public string? CallbackUrl { get; init; }

    /// <summary>TTL for Pattern 2 (shared across all recipients).</summary>
    public int? TtlSeconds { get; init; }

    /// <summary>Metadata for Pattern 2 (shared across all recipients).</summary>
    public Dictionary<string, string>? Metadata { get; init; }
}
