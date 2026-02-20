using Sinch.MessageRouter.Core;

namespace Sinch.MessageRouter.Channels;

/// <summary>
/// Defines a channel-specific adapter for sending messages via the Sinch Conversation API.
/// </summary>
public interface IChannelAdapter
{
    /// <summary>
    /// The channel type this adapter handles.
    /// </summary>
    ChannelType Channel { get; }

    /// <summary>
    /// Sends a message through this channel.
    /// </summary>
    Task<MessageResult> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default);
}
