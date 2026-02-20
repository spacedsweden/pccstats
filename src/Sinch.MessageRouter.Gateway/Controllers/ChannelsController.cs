using Microsoft.AspNetCore.Mvc;
using Sinch.MessageRouter.Core.Messages;

namespace Sinch.MessageRouter.Gateway.Controllers;

[ApiController]
[Route("v1/channels")]
public class ChannelsController : ControllerBase
{
    [HttpGet]
    public IActionResult List() => Ok(new { data = ChannelCatalog.All });

    [HttpGet("{channelId}")]
    public IActionResult Get(string channelId)
    {
        if (!Enum.TryParse<MessageChannel>(channelId, ignoreCase: true, out var channel))
            return NotFound();
        var info = ChannelCatalog.All.FirstOrDefault(c => c.Channel == channel);
        return info is null ? NotFound() : Ok(info);
    }
}

public record ChannelInfo(
    MessageChannel Channel, string DisplayName,
    bool SupportsRichCards, bool SupportsCarousel, bool SupportsMedia,
    bool SupportsLocation, bool SupportsChoices, bool SupportsTemplates,
    bool SupportsReadReceipts, int MaxTextLength);

public static class ChannelCatalog
{
    public static readonly IReadOnlyList<ChannelInfo> All = new ChannelInfo[]
    {
        new(MessageChannel.Sms, "SMS", false, false, false, false, false, true, false, 1600),
        new(MessageChannel.Mms, "MMS", false, false, true, false, false, false, false, 5000),
        new(MessageChannel.Rcs, "RCS", true, true, true, true, true, true, true, 10000),
        new(MessageChannel.WhatsApp, "WhatsApp", true, false, true, true, true, true, true, 4096),
        new(MessageChannel.Messenger, "Facebook Messenger", true, true, true, true, true, true, true, 2000),
        new(MessageChannel.Viber, "Viber", true, false, true, true, true, false, true, 1000),
        new(MessageChannel.Telegram, "Telegram", true, false, true, true, true, false, true, 4096),
        new(MessageChannel.Line, "LINE", true, true, true, true, true, false, true, 5000),
        new(MessageChannel.KakaoTalk, "KakaoTalk", true, false, true, false, true, true, false, 1000),
        new(MessageChannel.Instagram, "Instagram", false, false, true, false, false, false, true, 1000),
    };
}
