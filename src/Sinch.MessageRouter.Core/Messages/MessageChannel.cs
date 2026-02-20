using System.Text.Json.Serialization;

namespace Sinch.MessageRouter.Core.Messages;

/// <summary>
/// Supported messaging channels.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MessageChannel>))]
public enum MessageChannel
{
    Sms,
    Mms,
    Rcs,
    WhatsApp,
    Messenger,
    Viber,
    Telegram,
    Line,
    KakaoTalk,
    Instagram
}

/// <summary>
/// Delivery status of a message.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MessageStatus>))]
public enum MessageStatus
{
    Queued,
    Submitted,
    Delivered,
    Read,
    Failed,
    Revoked,
    Expired
}

/// <summary>
/// Message priority level.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<Priority>))]
public enum Priority
{
    Low,
    Normal,
    High
}
