using Microsoft.AspNetCore.Mvc;
using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Contacts;
using Sinch.MessageRouter.Core.Messages;
using Sinch.MessageRouter.Gateway.Services;

namespace Sinch.MessageRouter.Gateway.Controllers;

/// <summary>
/// Manage contacts and their channel identities.
/// Contacts link a person to their identifiers across channels (phone numbers, user IDs, etc.).
/// </summary>
[ApiController]
[Route("v1/contacts")]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contactService;
    private readonly ILogger<ContactsController> _logger;

    public ContactsController(IContactService contactService, ILogger<ContactsController> logger)
    {
        _contactService = contactService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new contact.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Contact), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] Contact contact, CancellationToken ct)
    {
        var result = await _contactService.CreateAsync(contact, ct);
        return CreatedAtAction(nameof(Get), new { contactId = result.Id }, result);
    }

    /// <summary>
    /// List contacts with cursor-based pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<Contact>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _contactService.ListAsync(cursor, Math.Clamp(pageSize, 1, 100), ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a contact by ID.
    /// </summary>
    [HttpGet("{contactId}")]
    [ProducesResponseType(typeof(Contact), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string contactId, CancellationToken ct)
    {
        var result = await _contactService.GetAsync(contactId, ct);
        if (result is null)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Contact '{contactId}' not found."
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Update a contact. Only provided fields are modified.
    /// </summary>
    [HttpPatch("{contactId}")]
    [ProducesResponseType(typeof(Contact), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string contactId, [FromBody] Contact contact, CancellationToken ct)
    {
        var result = await _contactService.UpdateAsync(contactId, contact, ct);
        if (result is null)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Contact '{contactId}' not found."
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Delete a contact.
    /// </summary>
    [HttpDelete("{contactId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string contactId, CancellationToken ct)
    {
        var success = await _contactService.DeleteAsync(contactId, ct);
        if (!success)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Contact '{contactId}' not found."
            });
        }
        return NoContent();
    }

    /// <summary>
    /// Get message history for a contact across all their channel identities.
    /// </summary>
    [HttpGet("{contactId}/messages")]
    [ProducesResponseType(typeof(PaginatedResponse<MessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(
        string contactId,
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _contactService.GetMessagesAsync(contactId, cursor, pageSize, ct);
        return Ok(result);
    }
}
