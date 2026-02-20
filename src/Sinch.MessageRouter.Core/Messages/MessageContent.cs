namespace Sinch.MessageRouter.Core.Messages;

/// <summary>
/// Rich message content. Uses a "type" discriminator with optional fields per type.
/// This flat structure is simpler than deep nesting while supporting all content types.
///
/// Types: text, media, card, carousel, location, template, choices, list, contact_info
///
/// Examples:
///   Text:     { "type": "text", "text": "Hello" }
///   Media:    { "type": "media", "mediaUrl": "https://...", "caption": "Photo" }
///   Card:     { "type": "card", "title": "Product", "description": "...", "actions": [...] }
///   Location: { "type": "location", "latitude": 59.33, "longitude": 18.07 }
///   Template: { "type": "template", "templateId": "welcome_v1", "templateParameters": { "name": "Alice" } }
/// </summary>
public sealed class MessageContent
{
    /// <summary>Content type discriminator.</summary>
    public required string Type { get; init; }

    // -- Text --
    /// <summary>Text body (for type: text, choices).</summary>
    public string? Text { get; init; }

    // -- Media --
    /// <summary>Media URL (for type: media, card).</summary>
    public string? MediaUrl { get; init; }

    /// <summary>MIME type of media.</summary>
    public string? MediaType { get; init; }

    /// <summary>Caption for media.</summary>
    public string? Caption { get; init; }

    /// <summary>File name for document media.</summary>
    public string? FileName { get; init; }

    /// <summary>Thumbnail URL for video/document previews.</summary>
    public string? ThumbnailUrl { get; init; }

    // -- Card --
    /// <summary>Card title (for type: card).</summary>
    public string? Title { get; init; }

    /// <summary>Card description (for type: card).</summary>
    public string? Description { get; init; }

    /// <summary>Interactive actions/buttons (for type: card, choices).</summary>
    public IReadOnlyList<CardAction>? Actions { get; init; }

    // -- Carousel --
    /// <summary>List of cards (for type: carousel).</summary>
    public IReadOnlyList<MessageContent>? Cards { get; init; }

    // -- Location --
    /// <summary>Latitude (for type: location).</summary>
    public double? Latitude { get; init; }

    /// <summary>Longitude (for type: location).</summary>
    public double? Longitude { get; init; }

    /// <summary>Location label/name (for type: location).</summary>
    public string? LocationLabel { get; init; }

    // -- Template --
    /// <summary>Template reference ID (for type: template).</summary>
    public string? TemplateId { get; init; }

    /// <summary>Language code for template (for type: template).</summary>
    public string? LanguageCode { get; init; }

    /// <summary>Template parameter values (for type: template).</summary>
    public Dictionary<string, string>? TemplateParameters { get; init; }

    // -- List --
    /// <summary>List sections (for type: list).</summary>
    public IReadOnlyList<ListSection>? Sections { get; init; }

    // -- Contact Info --
    /// <summary>Display name (for type: contact_info).</summary>
    public string? DisplayName { get; init; }

    /// <summary>Phone number (for type: contact_info).</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>Email address (for type: contact_info).</summary>
    public string? Email { get; init; }

    // -- Extension point --
    /// <summary>Additional channel-specific payload for advanced use cases.</summary>
    public Dictionary<string, object>? Extra { get; init; }
}

/// <summary>
/// Interactive button/action on a card or choices message.
/// </summary>
public sealed class CardAction
{
    /// <summary>Action type: reply, url, call, location, calendar.</summary>
    public required string Type { get; init; }

    /// <summary>Button display text.</summary>
    public required string Title { get; init; }

    /// <summary>Payload returned on tap (for type: reply).</summary>
    public string? Payload { get; init; }

    /// <summary>URL to open (for type: url).</summary>
    public string? Url { get; init; }

    /// <summary>Phone number to call (for type: call).</summary>
    public string? PhoneNumber { get; init; }
}

/// <summary>
/// A section in a list message.
/// </summary>
public sealed class ListSection
{
    /// <summary>Section title.</summary>
    public required string Title { get; init; }

    /// <summary>Items in this section.</summary>
    public required IReadOnlyList<ListItem> Items { get; init; }
}

/// <summary>
/// An item in a list section.
/// </summary>
public sealed class ListItem
{
    /// <summary>Item identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Item title.</summary>
    public required string Title { get; init; }

    /// <summary>Item description.</summary>
    public string? Description { get; init; }
}
