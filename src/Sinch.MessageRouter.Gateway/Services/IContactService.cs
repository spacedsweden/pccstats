using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Contacts;
using Sinch.MessageRouter.Core.Messages;

namespace Sinch.MessageRouter.Gateway.Services;

/// <summary>
/// Manages contacts and their message histories.
/// </summary>
public interface IContactService
{
    /// <summary>Creates a new contact.</summary>
    Task<Contact> CreateAsync(Contact contact, CancellationToken ct = default);

    /// <summary>Lists contacts with pagination.</summary>
    Task<PaginatedResponse<Contact>> ListAsync(string? cursor, int pageSize, CancellationToken ct = default);

    /// <summary>Gets a contact by ID.</summary>
    Task<Contact?> GetAsync(string contactId, CancellationToken ct = default);

    /// <summary>Updates a contact.</summary>
    Task<Contact?> UpdateAsync(string contactId, Contact contact, CancellationToken ct = default);

    /// <summary>Deletes a contact.</summary>
    Task<bool> DeleteAsync(string contactId, CancellationToken ct = default);

    /// <summary>Gets the message history for a contact.</summary>
    Task<PaginatedResponse<MessageResponse>> GetMessagesAsync(
        string contactId, string? cursor, int pageSize, CancellationToken ct = default);
}
