using Microsoft.AspNetCore.Mvc;
using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Messages;

namespace Sinch.MessageRouter.Gateway.Controllers;

/// <summary>
/// Provides information about available messaging channels and their capabilities.
/// </summary>
[ApiController]
[Route("v1/channels")]
public class ChannelsController : ControllerBase
{
    private readonly IConfiguration _config;

    /// <summary>
    /// Static channel capability catalog.
    /// </summary>
    private static readonly IReadOnlyList<ChannelInfo> ChannelCatalog =
    [
        new()
        {
            Id = "sms", DisplayName = "SMS", Channel = MessageChannel.Sms,
            SupportsRichCards = false, SupportsCarousel = false, SupportsMedia = false,
            SupportsLocation = false, SupportsChoices = false, SupportsTemplates = true,
            SupportsReadReceipts = false, MaxTextLength = 1600,
            SupportedContentTypes = ["text"]
        },
        new()
        {
            Id = "mms", DisplayName = "MMS", Channel = MessageChannel.Mms,
            SupportsRichCards = false, SupportsCarousel = false, SupportsMedia = true,
            SupportsLocation = false, SupportsChoices = false, SupportsTemplates = false,
            SupportsReadReceipts = false, MaxTextLength = 5000,
            SupportedContentTypes = ["text", "media"]
        },
        new()
        {
            Id = "rcs", DisplayName = "RCS", Channel = MessageChannel.Rcs,
            SupportsRichCards = true, SupportsCarousel = true, SupportsMedia = true,
            SupportsLocation = true, SupportsChoices = true, SupportsTemplates = true,
            SupportsReadReceipts = true, MaxTextLength = 10000,
            SupportedContentTypes = ["text", "media", "card", "carousel", "choices", "location"]
        },
        new()
        {
            Id = "whatsapp", DisplayName = "WhatsApp", Channel = MessageChannel.WhatsApp,
            SupportsRichCards = true, SupportsCarousel = false, SupportsMedia = true,
            SupportsLocation = true, SupportsChoices = true, SupportsTemplates = true,
            SupportsReadReceipts = true, MaxTextLength = 4096,
            SupportedContentTypes = ["text", "media", "template", "location", "contact_info", "list", "choices"]
        },
        new()
        {
            Id = "messenger", DisplayName = "Facebook Messenger", Channel = MessageChannel.Messenger,
            SupportsRichCards = true, SupportsCarousel = true, SupportsMedia = true,
            SupportsLocation = true, SupportsChoices = true, SupportsTemplates = true,
            SupportsReadReceipts = true, MaxTextLength = 2000,
            SupportedContentTypes = ["text", "media", "card", "carousel", "choices"]
        },
        new()
        {
            Id = "viber", DisplayName = "Viber", Channel = MessageChannel.Viber,
            SupportsRichCards = true, SupportsCarousel = false, SupportsMedia = true,
            SupportsLocation = true, SupportsChoices = true, SupportsTemplates = false,
            SupportsReadReceipts = true, MaxTextLength = 1000,
            SupportedContentTypes = ["text", "media", "card"]
        },
        new()
        {
            Id = "telegram", DisplayName = "Telegram", Channel = MessageChannel.Telegram,
            SupportsRichCards = true, SupportsCarousel = false, SupportsMedia = true,
            SupportsLocation = true, SupportsChoices = true, SupportsTemplates = false,
            SupportsReadReceipts = true, MaxTextLength = 4096,
            SupportedContentTypes = ["text", "media", "location", "contact_info"]
        },
        new()
        {
            Id = "line", DisplayName = "LINE", Channel = MessageChannel.Line,
            SupportsRichCards = true, SupportsCarousel = true, SupportsMedia = true,
            SupportsLocation = true, SupportsChoices = true, SupportsTemplates = false,
            SupportsReadReceipts = true, MaxTextLength = 5000,
            SupportedContentTypes = ["text", "media", "card", "carousel", "location"]
        },
        new()
        {
            Id = "kakaotalk", DisplayName = "KakaoTalk", Channel = MessageChannel.KakaoTalk,
            SupportsRichCards = true, SupportsCarousel = false, SupportsMedia = true,
            SupportsLocation = false, SupportsChoices = true, SupportsTemplates = true,
            SupportsReadReceipts = false, MaxTextLength = 1000,
            SupportedContentTypes = ["text", "media", "template"]
        },
        new()
        {
            Id = "instagram", DisplayName = "Instagram", Channel = MessageChannel.Instagram,
            SupportsRichCards = false, SupportsCarousel = false, SupportsMedia = true,
            SupportsLocation = false, SupportsChoices = false, SupportsTemplates = false,
            SupportsReadReceipts = true, MaxTextLength = 1000,
            SupportedContentTypes = ["text", "media"]
        }
    ];

    public ChannelsController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// List all available messaging channels with their capabilities.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ChannelInfo>), StatusCodes.Status200OK)]
    public IActionResult List()
    {
        var enriched = ChannelCatalog.Select(c => c with
        {
            DefaultSender = _config[$"ChannelDefaults:{c.Channel}:DefaultSender"]
        }).ToList();

        return Ok(enriched);
    }

    /// <summary>
    /// Get detailed information about a specific channel.
    /// </summary>
    /// <param name="channelId">The channel identifier (e.g., "sms", "whatsapp").</param>
    [HttpGet("{channelId}")]
    [ProducesResponseType(typeof(ChannelInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Get(string channelId)
    {
        var channel = ChannelCatalog.FirstOrDefault(c =>
            string.Equals(c.Id, channelId, StringComparison.OrdinalIgnoreCase));

        if (channel is null)
        {
            // Also try matching by enum name
            if (Enum.TryParse<MessageChannel>(channelId, ignoreCase: true, out var parsed))
                channel = ChannelCatalog.FirstOrDefault(c => c.Channel == parsed);
        }

        if (channel is null)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Channel '{channelId}' not found."
            });
        }

        var enriched = channel with
        {
            DefaultSender = _config[$"ChannelDefaults:{channel.Channel}:DefaultSender"]
        };

        return Ok(enriched);
    }
}

/// <summary>
/// Information about a messaging channel and its capabilities.
/// </summary>
public sealed record ChannelInfo
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required MessageChannel Channel { get; init; }
    public bool SupportsRichCards { get; init; }
    public bool SupportsCarousel { get; init; }
    public bool SupportsMedia { get; init; }
    public bool SupportsLocation { get; init; }
    public bool SupportsChoices { get; init; }
    public bool SupportsTemplates { get; init; }
    public bool SupportsReadReceipts { get; init; }
    public int MaxTextLength { get; init; }
    public IReadOnlyList<string>? SupportedContentTypes { get; init; }
    public string? DefaultSender { get; init; }
}
