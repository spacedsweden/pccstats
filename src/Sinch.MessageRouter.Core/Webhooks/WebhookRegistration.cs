namespace Sinch.MessageRouter.Core.Webhooks;

/// <summary>
/// Represents a registered webhook endpoint.
/// </summary>
public sealed class WebhookRegistration
{
    /// <summary>Unique webhook registration ID.</summary>
    public string? Id { get; set; }

    /// <summary>The URL to deliver events to.</summary>
    public required string Url { get; set; }

    /// <summary>Event types this webhook subscribes to (e.g. "message.delivered").</summary>
    public IReadOnlyList<string>? Events { get; set; }

    /// <summary>Shared secret for HMAC signature verification.</summary>
    public string? Secret { get; set; }

    /// <summary>Custom headers to include in webhook deliveries.</summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>Whether this webhook is active.</summary>
    public bool Active { get; set; } = true;

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Last update timestamp.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
