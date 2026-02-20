namespace Sinch.MessageRouter.Webhooks;

/// <summary>
/// Handles inbound webhook events from the Sinch Conversation API.
/// </summary>
public interface IWebhookHandler
{
    /// <summary>
    /// Processes a raw webhook payload.
    /// </summary>
    Task HandleAsync(string payload, CancellationToken cancellationToken = default);
}
