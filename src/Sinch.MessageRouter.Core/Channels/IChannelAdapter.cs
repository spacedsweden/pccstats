using Sinch.MessageRouter.Core.Messages;

namespace Sinch.MessageRouter.Core.Channels;

/// <summary>
/// Defines a channel-specific adapter that transforms generic message
/// requests into channel-specific Sinch API payloads.
/// </summary>
public interface IChannelAdapter
{
    /// <summary>
    /// The messaging channel this adapter handles.
    /// </summary>
    MessageChannel Channel { get; }

    /// <summary>
    /// Transforms a generic SendMessageRequest into a channel-specific
    /// Sinch Conversation API request body.
    /// </summary>
    /// <param name="request">The generic send request.</param>
    /// <returns>A dictionary representing the Sinch API request body.</returns>
    Dictionary<string, object> TransformRequest(SendMessageRequest request);

    /// <summary>
    /// Returns the default sender identity for this channel, if configured.
    /// </summary>
    string? GetDefaultSender();
}
