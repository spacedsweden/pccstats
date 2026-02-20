using System.Text.Json.Serialization;
using Sinch.MessageRouter.Core.Messages;

namespace Sinch.MessageRouter.Core.Dispatch;

/// <summary>
/// Routing configuration for multi-channel message delivery.
/// Defines how messages are routed across channels — this replaces and simplifies
/// the Sinch Conversation API dispatch mode.
/// </summary>
public class RoutingSettings
{
    /// <summary>
    /// Routing strategy.
    /// - Failover: Try routes in order until one succeeds
    /// - Broadcast: Send on all routes simultaneously
    /// - RoundRobin: Distribute across routes evenly
    /// - CostOptimized: Pick cheapest available route
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<RoutingStrategy>))]
    public required RoutingStrategy Strategy { get; init; }

    /// <summary>
    /// Ordered list of channel routes to attempt.
    /// </summary>
    public required IReadOnlyList<ChannelRoute> Routes { get; init; }

    /// <summary>
    /// Time in seconds to wait for each route before trying the next (failover only).
    /// </summary>
    public int? TimeoutPerRoute { get; init; }

    /// <summary>
    /// Whether to continue trying remaining routes after first success (failover only).
    /// Default false — stops after first success.
    /// </summary>
    public bool? ContinueAfterSuccess { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<RoutingStrategy>))]
public enum RoutingStrategy
{
    Failover,
    Broadcast,
    RoundRobin,
    CostOptimized
}

public class ChannelRoute
{
    /// <summary>Channel for this route.</summary>
    public required MessageChannel Channel { get; init; }

    /// <summary>Override message body for this channel.</summary>
    public string? Body { get; init; }

    /// <summary>Override rich content for this channel.</summary>
    public MessageContent? Content { get; init; }

    /// <summary>Override sender for this channel.</summary>
    public string? From { get; init; }

    /// <summary>Weight for round-robin distribution (default 1).</summary>
    public int Weight { get; init; } = 1;

    /// <summary>Maximum cost threshold for this route (cost-optimized).</summary>
    public decimal? MaxCost { get; init; }
}
