using System.Collections.Concurrent;
using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Contacts;
using Sinch.MessageRouter.Core.Messages;

namespace Sinch.MessageRouter.Gateway.Services;

/// <summary>
/// In-memory contact manager with message history tracking.
/// Replace with a database-backed implementation in production.
/// </summary>
public sealed class ContactService : IContactService
{
    private readonly ConcurrentDictionary<string, Contact> _contacts = new();
    private readonly IMessageService _messageService;
    private readonly ILogger<ContactService> _logger;

    public ContactService(IMessageService messageService, ILogger<ContactService> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Contact> CreateAsync(Contact contact, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(contact.DisplayName))
            throw new ArgumentException("Contact display name is required.");

        if (contact.ChannelIdentities is not { Count: > 0 })
            throw new ArgumentException("At least one channel identity is required.");

        var id = Guid.NewGuid().ToString("N")[..16];
        var created = new Contact
        {
            Id = id,
            DisplayName = contact.DisplayName,
            ExternalId = contact.ExternalId,
            ChannelIdentities = contact.ChannelIdentities,
            Email = contact.Email,
            Metadata = contact.Metadata,
            Language = contact.Language,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _contacts[id] = created;
        _logger.LogInformation("Contact {ContactId} '{DisplayName}' created", id, contact.DisplayName);

        return Task.FromResult(created);
    }

    /// <inheritdoc />
    public Task<PaginatedResponse<Contact>> ListAsync(string? cursor, int pageSize, CancellationToken ct)
    {
        var contacts = _contacts.Values
            .OrderByDescending(c => c.CreatedAt)
            .ToList();

        if (!string.IsNullOrEmpty(cursor))
        {
            var cursorIndex = contacts.FindIndex(c => c.Id == cursor);
            if (cursorIndex >= 0)
                contacts = contacts.Skip(cursorIndex + 1).ToList();
        }

        pageSize = Math.Clamp(pageSize, 1, 100);
        var page = contacts.Take(pageSize).ToList();
        var hasMore = contacts.Count > pageSize;

        return Task.FromResult(new PaginatedResponse<Contact>
        {
            Items = page,
            NextCursor = hasMore && page.Count > 0 ? page[^1].Id : null,
            TotalCount = _contacts.Count,
            PageSize = pageSize
        });
    }

    /// <inheritdoc />
    public Task<Contact?> GetAsync(string contactId, CancellationToken ct)
    {
        _contacts.TryGetValue(contactId, out var contact);
        return Task.FromResult(contact);
    }

    /// <inheritdoc />
    public Task<Contact?> UpdateAsync(string contactId, Contact contact, CancellationToken ct)
    {
        if (!_contacts.TryGetValue(contactId, out var existing))
            return Task.FromResult<Contact?>(null);

        var updated = new Contact
        {
            Id = existing.Id,
            DisplayName = contact.DisplayName ?? existing.DisplayName,
            ExternalId = contact.ExternalId ?? existing.ExternalId,
            ChannelIdentities = contact.ChannelIdentities ?? existing.ChannelIdentities,
            Email = contact.Email ?? existing.Email,
            Metadata = contact.Metadata ?? existing.Metadata,
            Language = contact.Language ?? existing.Language,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _contacts[contactId] = updated;
        _logger.LogInformation("Contact {ContactId} updated", contactId);

        return Task.FromResult<Contact?>(updated);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string contactId, CancellationToken ct)
    {
        var removed = _contacts.TryRemove(contactId, out _);
        if (removed)
            _logger.LogInformation("Contact {ContactId} deleted", contactId);
        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public async Task<PaginatedResponse<MessageResponse>> GetMessagesAsync(
        string contactId, string? cursor, int pageSize, CancellationToken ct)
    {
        if (!_contacts.TryGetValue(contactId, out var contact))
            throw new KeyNotFoundException($"Contact '{contactId}' not found.");

        // Query messages for all channel identities of this contact
        var allMessages = new List<MessageResponse>();

        foreach (var identity in contact.ChannelIdentities)
        {
            var query = new MessageListQuery
            {
                To = identity.Identity,
                PageSize = 100 // Fetch a reasonable batch
            };
            var result = await _messageService.ListAsync(query, ct);
            allMessages.AddRange(result.Items);
        }

        // Deduplicate and sort
        var sorted = allMessages
            .DistinctBy(m => m.MessageId)
            .OrderByDescending(m => m.CreatedAt)
            .ToList();

        // Apply cursor
        if (!string.IsNullOrEmpty(cursor))
        {
            var cursorIndex = sorted.FindIndex(m => m.MessageId == cursor);
            if (cursorIndex >= 0)
                sorted = sorted.Skip(cursorIndex + 1).ToList();
        }

        pageSize = Math.Clamp(pageSize, 1, 100);
        var page = sorted.Take(pageSize).ToList();

        return new PaginatedResponse<MessageResponse>
        {
            Items = page,
            NextCursor = sorted.Count > pageSize && page.Count > 0 ? page[^1].MessageId : null,
            TotalCount = sorted.Count,
            PageSize = pageSize
        };
    }
}
