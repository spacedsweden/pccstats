using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Messages;

namespace Sinch.MessageRouter.Gateway.Services;

/// <summary>
/// Service for sending and retrieving messages through the Sinch Conversation API.
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// Sends a single message, transforming the request into the Sinch Conversation API format.
    /// Handles dispatch/fallback logic when a dispatch chain is provided.
    /// </summary>
    Task<MessageResponse> SendAsync(SendMessageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sends a batch of messages, returning individual results for each message.
    /// </summary>
    Task<BatchMessageResponse> SendBatchAsync(BatchSendRequest request, CancellationToken ct = default);

    /// <summary>
    /// Lists messages matching the given query parameters.
    /// </summary>
    Task<PaginatedResponse<MessageResponse>> ListAsync(MessageListQuery query, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single message by its ID.
    /// </summary>
    Task<MessageResponse?> GetAsync(string messageId, CancellationToken ct = default);

    /// <summary>
    /// Revokes (cancels) a previously sent message if it has not yet been delivered.
    /// </summary>
    Task<bool> RevokeAsync(string messageId, CancellationToken ct = default);
}
