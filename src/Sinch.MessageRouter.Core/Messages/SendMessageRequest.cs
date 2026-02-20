namespace Sinch.MessageRouter.Core.Messages;

/// <summary>
/// Send a message with progressive complexity:
///
/// Level 1 - SMS (simplest, just "to" and "body"):
///   POST /v1/messages { "to": "+1234567890", "body": "Hello" }
///
/// Level 2 - Specify channel:
///   POST /v1/messages { "to": "+1234567890", "channel": "whatsapp", "body": "Hello" }
///
/// Level 3 - Rich content:
///   POST /v1/messages { "to": "+1234567890", "channel": "rcs", "content": { "type": "card", ... } }
///
/// Level 4 - Multi-channel routing:
///   POST /v1/messages { "to": "+1234567890", "routes": [{ "channel": "rcs", ... }, { "channel": "sms", ... }] }
/// </summary>
public sealed class SendMessageRequest
{
    /// <summary>Recipient phone number (E.164) or channel-specific identifier.</summary>
    public required string To { get; init; }

    /// <summary>Sender ID. If omitted, uses the default sender for the channel.</summary>
    public string? From { get; init; }

    /// <summary>
    /// Simple text body. Use this for plain text messages.
    /// If both Body and Content are provided, Content takes precedence.
    /// If only Body is provided without Channel, defaults to SMS.
    /// </summary>
    public string? Body { get; init; }

    /// <summary>Target channel. Defaults to SMS if omitted.</summary>
    public MessageChannel? Channel { get; init; }

    /// <summary>Rich content (cards, media, carousel, location, template, choices). Use instead of Body.</summary>
    public MessageContent? Content { get; init; }

    /// <summary>
    /// Multi-channel routing with fallback. Each route specifies a channel to try.
    /// Routes are attempted in order (failover) — if the first channel fails,
    /// the next one is tried automatically.
    /// When set, Channel and Body/Content are ignored; each route defines its own.
    /// Mutually exclusive with RoutingRule.
    /// </summary>
    public IReadOnlyList<MessageRoute>? Routes { get; init; }

    /// <summary>
    /// Reference a saved routing rule by ID instead of inlining routes.
    /// The rule's strategy and routes will be applied to this message.
    /// Mutually exclusive with Routes.
    /// </summary>
    public string? RoutingRule { get; init; }

    /// <summary>Callback URL for delivery status updates on this message.</summary>
    public string? CallbackUrl { get; init; }

    /// <summary>TTL in seconds. Message expires if not delivered within this time.</summary>
    public int? TtlSeconds { get; init; }

    /// <summary>Message priority.</summary>
    public Priority Priority { get; init; } = Priority.Normal;

    /// <summary>Arbitrary metadata key-value pairs attached to the message.</summary>
    public Dictionary<string, string>? Metadata { get; init; }

    /// <summary>Correlation ID for idempotency and tracing.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Resolve the effective channel (defaults to SMS).</summary>
    public MessageChannel EffectiveChannel => Channel ?? MessageChannel.Sms;

    /// <summary>Resolve the effective content from Body or Content.</summary>
    public MessageContent? EffectiveContent => Content ?? (Body != null ? new MessageContent { Type = "text", Text = Body } : null);
}

/// <summary>
/// A channel route in a multi-channel message.
/// Routes are tried in order — if the message fails on the first channel,
/// the next route is attempted automatically.
/// </summary>
public sealed class MessageRoute
{
    /// <summary>Channel for this route.</summary>
    public required MessageChannel Channel { get; init; }

    /// <summary>Override text body for this channel.</summary>
    public string? Body { get; init; }

    /// <summary>Override rich content for this channel.</summary>
    public MessageContent? Content { get; init; }

    /// <summary>Override sender for this channel.</summary>
    public string? From { get; init; }

    /// <summary>Timeout in seconds before falling back to the next route.</summary>
    public int? TimeoutSeconds { get; init; }
}
