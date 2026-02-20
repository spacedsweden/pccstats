using Sinch.MessageRouter.Core.Messages;

namespace Sinch.MessageRouter.Core.Contacts;

public class Contact
{
    public string? Id { get; init; }
    public required string DisplayName { get; init; }
    public string? ExternalId { get; init; }
    public required IReadOnlyList<ChannelIdentity> ChannelIdentities { get; init; }
    public string? Email { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
    public string? Language { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public class ChannelIdentity
{
    public required MessageChannel Channel { get; init; }
    public required string Identity { get; init; }
    public string? AppId { get; init; }
}

public class CreateContactRequest
{
    public required string DisplayName { get; init; }
    public string? ExternalId { get; init; }
    public required IReadOnlyList<ChannelIdentity> ChannelIdentities { get; init; }
    public string? Email { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
    public string? Language { get; init; }
}

public class UpdateContactRequest
{
    public string? DisplayName { get; init; }
    public string? ExternalId { get; init; }
    public IReadOnlyList<ChannelIdentity>? ChannelIdentities { get; init; }
    public string? Email { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
    public string? Language { get; init; }
}
