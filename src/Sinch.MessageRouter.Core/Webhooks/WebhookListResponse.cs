namespace Sinch.MessageRouter.Core.Webhooks;

public sealed class WebhookListResponse
{
    public required IReadOnlyList<WebhookRegistration> Data { get; init; }
}
