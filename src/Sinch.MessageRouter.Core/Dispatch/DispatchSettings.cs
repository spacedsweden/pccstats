using System.Text.Json.Serialization;
using Sinch.MessageRouter.Core.Messages;

namespace Sinch.MessageRouter.Core.Dispatch;

/// <summary>
/// Dispatch configuration for multi-channel message routing.
/// This is the "hostile redesign" of Sinch's dispatch mode -
/// much simpler while retaining all functionality.
/// </summary>
public class DispatchSettings
{
    /// <summary>
    /// Routing strategy.
    /// - failover: Try routes in order until one succeeds
    /// - broadcast: Send on all routes simultaneously
    /// - round-robin: Distribute across routes evenly
    /// - cost-optimized: Pick cheapest available route
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<DispatchStrategy>))]
    public required DispatchStrategy Strategy { get; init; }

    /// <summary>
    /// Ordered list of channel routes to attempt.
    /// </summary>
    public required IReadOnlyList<DispatchRoute> Routes { get; init; }

    /// <summary>
    /// Time in seconds to wait for each route before trying the next (failover only).
    /// </summary>
    public int? TimeoutPerRoute { get; init; }

    /// <summary>
    /// Whether to continue trying remaining routes after first success (failover only).
    /// Default false - stops after first success.
    /// </summary>
    public bool? ContinueAfterSuccess { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<DispatchStrategy>))]
public enum DispatchStrategy
{
    Failover,
    Broadcast,
    RoundRobin,
    CostOptimized
}

public class DispatchRoute
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
