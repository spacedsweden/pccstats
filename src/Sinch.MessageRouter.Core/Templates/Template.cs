using Sinch.MessageRouter.Core.Messages;

namespace Sinch.MessageRouter.Core.Templates;

public class Template
{
    public string? Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required MessageChannel Channel { get; init; }
    public required string Body { get; init; }
    public IReadOnlyList<string>? ParameterNames { get; init; }
    public string? Language { get; init; }
    public TemplateStatus Status { get; init; } = TemplateStatus.Draft;
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public enum TemplateStatus
{
    Draft,
    Pending,
    Approved,
    Rejected
}

public class CreateTemplateRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required MessageChannel Channel { get; init; }
    public required string Body { get; init; }
    public IReadOnlyList<string>? ParameterNames { get; init; }
    public string? Language { get; init; }
}

public class UpdateTemplateRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Body { get; init; }
    public IReadOnlyList<string>? ParameterNames { get; init; }
    public string? Language { get; init; }
}
