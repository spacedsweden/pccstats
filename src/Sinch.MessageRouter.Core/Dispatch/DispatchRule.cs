namespace Sinch.MessageRouter.Core.Dispatch;

/// <summary>
/// A saved routing rule that can be reused across messages.
/// Instead of inlining routes on every message, save a rule and reference it by ID.
/// </summary>
public class RoutingRule
{
    public string? Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required RoutingSettings Settings { get; init; }
    public bool Active { get; init; } = true;
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public class CreateRoutingRuleRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required RoutingSettings Settings { get; init; }
}

public class UpdateRoutingRuleRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public RoutingSettings? Settings { get; init; }
    public bool? Active { get; init; }
}
