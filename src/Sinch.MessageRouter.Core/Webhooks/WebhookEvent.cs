using System.Text.Json.Serialization;
using Sinch.MessageRouter.Core.Messages;

namespace Sinch.MessageRouter.Core.Webhooks;

/// <summary>
/// Base class for all webhook events emitted by the message router.
/// </summary>
[JsonDerivedType(typeof(MessageDeliveredEvent), "message.delivered")]
[JsonDerivedType(typeof(MessageReadEvent), "message.read")]
[JsonDerivedType(typeof(MessageFailedEvent), "message.failed")]
[JsonDerivedType(typeof(MessageSubmittedEvent), "message.submitted")]
[JsonDerivedType(typeof(RoutingCompletedEvent), "routing.completed")]
[JsonDerivedType(typeof(RoutingFailedEvent), "routing.failed")]
public abstract class WebhookEvent
{
    /// <summary>Unique event ID.</summary>
    public required string EventId { get; init; }

    /// <summary>Event type discriminator (e.g. "message.delivered").</summary>
    public required string EventType { get; init; }

    /// <summary>Timestamp when the event occurred.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>The project / app ID this event belongs to.</summary>
    public string? ProjectId { get; init; }
}

// ── Message lifecycle events ────────────────────────────────────────

/// <summary>
/// Fired when a message has been delivered to the recipient's device.
/// </summary>
public class MessageDeliveredEvent : WebhookEvent
{
    public required string MessageId { get; init; }
    public required MessageChannel Channel { get; init; }
    public required string To { get; init; }
    public string? From { get; init; }
}

/// <summary>
/// Fired when a message has been read by the recipient (channels that support read receipts).
/// </summary>
public class MessageReadEvent : WebhookEvent
{
    public required string MessageId { get; init; }
    public required MessageChannel Channel { get; init; }
    public required string To { get; init; }
    public string? From { get; init; }
}

/// <summary>
/// Fired when a message send has permanently failed.
/// </summary>
public class MessageFailedEvent : WebhookEvent
{
    public required string MessageId { get; init; }
    public required MessageChannel Channel { get; init; }
    public required string To { get; init; }
    public string? From { get; init; }
    public required string ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Fired when a message has been submitted to the downstream channel provider.
/// </summary>
public class MessageSubmittedEvent : WebhookEvent
{
    public required string MessageId { get; init; }
    public required MessageChannel Channel { get; init; }
    public required string To { get; init; }
    public string? From { get; init; }
}

// ── Routing lifecycle events ────────────────────────────────────────

/// <summary>
/// Fired when multi-channel routing has completed successfully (at least one route succeeded).
/// </summary>
public class RoutingCompletedEvent : WebhookEvent
{
    public required string MessageId { get; init; }
    public required MessageChannel SuccessfulChannel { get; init; }
    public int RoutesAttempted { get; init; }
}

/// <summary>
/// Fired when multi-channel routing has exhausted all routes without success.
/// </summary>
public class RoutingFailedEvent : WebhookEvent
{
    public required string MessageId { get; init; }
    public int RoutesAttempted { get; init; }
    public string? LastErrorCode { get; init; }
    public string? LastErrorMessage { get; init; }
}
