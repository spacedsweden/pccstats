namespace Sinch.MessageRouter.Core.Webhooks;

public class CreateWebhookRequest
{
    public required string Url { get; init; }
    public IReadOnlyList<string>? Events { get; init; }
    public string? Secret { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
}

public class UpdateWebhookRequest
{
    public string? Url { get; init; }
    public IReadOnlyList<string>? Events { get; init; }
    public string? Secret { get; init; }
    public bool? Active { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
}
