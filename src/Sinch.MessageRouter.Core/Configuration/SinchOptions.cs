namespace Sinch.MessageRouter.Core.Configuration;

/// <summary>
/// Configuration options for the Sinch Conversation API connection.
/// </summary>
public sealed class SinchOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Sinch";

    /// <summary>Sinch project ID.</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Sinch app ID for the Conversation API.</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>Access key ID for authentication.</summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>Access key secret for authentication.</summary>
    public string AccessKeySecret { get; set; } = string.Empty;

    /// <summary>Base URL for the Sinch Conversation API.</summary>
    public string BaseUrl { get; set; } = "https://us.conversation.api.sinch.com";

    /// <summary>Region code (us, eu, etc.).</summary>
    public string Region { get; set; } = "us";
}
