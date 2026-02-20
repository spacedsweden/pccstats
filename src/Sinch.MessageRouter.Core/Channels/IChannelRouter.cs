using Sinch.MessageRouter.Core.Messages;

namespace Sinch.MessageRouter.Core.Channels;

/// <summary>
/// Routes messages to the appropriate channel adapter.
/// </summary>
public interface IChannelRouter
{
    /// <summary>
    /// Retrieves the channel adapter for the specified channel.
    /// </summary>
    /// <param name="channel">The messaging channel.</param>
    /// <returns>The adapter, or null if the channel is not supported.</returns>
    IChannelAdapter? GetAdapter(MessageChannel channel);

    /// <summary>
    /// Returns all registered channel adapters.
    /// </summary>
    IReadOnlyList<IChannelAdapter> GetAllAdapters();
}
