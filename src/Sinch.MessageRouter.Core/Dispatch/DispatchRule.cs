namespace Sinch.MessageRouter.Core.Dispatch;

/// <summary>
/// A saved dispatch rule that can be reused across messages.
/// </summary>
public class DispatchRule
{
    public string? Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required DispatchSettings Settings { get; init; }
    public bool Active { get; init; } = true;
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public class CreateDispatchRuleRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required DispatchSettings Settings { get; init; }
}

public class UpdateDispatchRuleRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public DispatchSettings? Settings { get; init; }
    public bool? Active { get; init; }
}
