using Microsoft.AspNetCore.Mvc;
using Sinch.MessageRouter.Core.Contacts;
using System.Collections.Concurrent;

namespace Sinch.MessageRouter.Gateway.Controllers;

/// <summary>
/// Manage contacts and their channel identities.
/// Contacts link a person to their identifiers across channels.
/// </summary>
[ApiController]
[Route("v1/contacts")]
public class ContactsController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, Contact> Contacts = new();

    [HttpPost]
    [ProducesResponseType(typeof(Contact), StatusCodes.Status201Created)]
    public IActionResult Create([FromBody] CreateContactRequest request)
    {
        var contact = new Contact
        {
            Id = Guid.NewGuid().ToString("N")[..16],
            DisplayName = request.DisplayName,
            ExternalId = request.ExternalId,
            ChannelIdentities = request.ChannelIdentities,
            Email = request.Email,
            Metadata = request.Metadata,
            Language = request.Language,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        Contacts[contact.Id!] = contact;
        return CreatedAtAction(nameof(Get), new { contactId = contact.Id }, contact);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ContactListResponse), StatusCodes.Status200OK)]
    public IActionResult List()
    {
        return Ok(new ContactListResponse { Data = Contacts.Values.ToList() });
    }

    [HttpGet("{contactId}")]
    [ProducesResponseType(typeof(Contact), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Get(string contactId)
    {
        return Contacts.TryGetValue(contactId, out var contact) ? Ok(contact) : NotFound();
    }

    [HttpPatch("{contactId}")]
    [ProducesResponseType(typeof(Contact), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Update(string contactId, [FromBody] UpdateContactRequest request)
    {
        if (!Contacts.TryGetValue(contactId, out var existing))
            return NotFound();

        var updated = new Contact
        {
            Id = existing.Id,
            DisplayName = request.DisplayName ?? existing.DisplayName,
            ExternalId = request.ExternalId ?? existing.ExternalId,
            ChannelIdentities = request.ChannelIdentities ?? existing.ChannelIdentities,
            Email = request.Email ?? existing.Email,
            Metadata = request.Metadata ?? existing.Metadata,
            Language = request.Language ?? existing.Language,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        Contacts[contactId] = updated;
        return Ok(updated);
    }

    [HttpDelete("{contactId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(string contactId)
    {
        return Contacts.TryRemove(contactId, out _) ? NoContent() : NotFound();
    }
}

public class ContactListResponse
{
    public required IReadOnlyList<Contact> Data { get; init; }
}
