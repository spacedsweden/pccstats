using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Webhooks;

namespace Sinch.MessageRouter.Gateway.Services;

/// <summary>
/// Manages webhook registrations for receiving event notifications.
/// </summary>
public interface IWebhookService
{
    /// <summary>Registers a new webhook endpoint.</summary>
    Task<WebhookRegistration> CreateAsync(CreateWebhookRequest request, CancellationToken ct = default);

    /// <summary>Lists all registered webhooks.</summary>
    Task<PaginatedResponse<WebhookRegistration>> ListAsync(string? cursor, int pageSize, CancellationToken ct = default);

    /// <summary>Gets a webhook by its ID.</summary>
    Task<WebhookRegistration?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>Updates a webhook registration.</summary>
    Task<WebhookRegistration?> UpdateAsync(string id, UpdateWebhookRequest request, CancellationToken ct = default);

    /// <summary>Deletes a webhook registration.</summary>
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>Sends a test event to the specified webhook.</summary>
    Task<WebhookTestResult> TestAsync(string id, CancellationToken ct = default);
}

/// <summary>
/// Result of a webhook test delivery.
/// </summary>
public sealed class WebhookTestResult
{
    public required bool Success { get; init; }
    public int? StatusCode { get; init; }
    public string? ResponseBody { get; init; }
    public string? ErrorMessage { get; init; }
    public long? LatencyMs { get; init; }
}
