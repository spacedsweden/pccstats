using System.Collections.Concurrent;
using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Templates;

namespace Sinch.MessageRouter.Gateway.Services;

/// <summary>
/// In-memory template manager. Handles CRUD for message templates.
/// Replace with a database-backed implementation in production.
/// </summary>
public sealed class TemplateService : ITemplateService
{
    private readonly ConcurrentDictionary<string, Template> _templates = new();
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(ILogger<TemplateService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Template> CreateAsync(CreateTemplateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Template name is required.");

        if (string.IsNullOrWhiteSpace(request.Body))
            throw new ArgumentException("Template body is required.");

        var id = Guid.NewGuid().ToString("N")[..16];
        var template = new Template
        {
            Id = id,
            Name = request.Name,
            Description = request.Description,
            Channel = request.Channel,
            Body = request.Body,
            ParameterNames = request.ParameterNames,
            Language = request.Language,
            Status = TemplateStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _templates[id] = template;
        _logger.LogInformation(
            "Template {TemplateId} '{TemplateName}' created for channel {Channel}",
            id, request.Name, request.Channel);

        return Task.FromResult(template);
    }

    /// <inheritdoc />
    public Task<PaginatedResponse<Template>> ListAsync(string? cursor, int pageSize, CancellationToken ct)
    {
        var templates = _templates.Values
            .OrderByDescending(t => t.CreatedAt)
            .ToList();

        if (!string.IsNullOrEmpty(cursor))
        {
            var cursorIndex = templates.FindIndex(t => t.Id == cursor);
            if (cursorIndex >= 0)
                templates = templates.Skip(cursorIndex + 1).ToList();
        }

        pageSize = Math.Clamp(pageSize, 1, 100);
        var page = templates.Take(pageSize).ToList();
        var hasMore = templates.Count > pageSize;

        return Task.FromResult(new PaginatedResponse<Template>
        {
            Items = page,
            NextCursor = hasMore && page.Count > 0 ? page[^1].Id : null,
            TotalCount = _templates.Count,
            PageSize = pageSize
        });
    }

    /// <inheritdoc />
    public Task<Template?> GetAsync(string id, CancellationToken ct)
    {
        _templates.TryGetValue(id, out var template);
        return Task.FromResult(template);
    }

    /// <inheritdoc />
    public Task<Template?> UpdateAsync(string id, UpdateTemplateRequest request, CancellationToken ct)
    {
        if (!_templates.TryGetValue(id, out var existing))
            return Task.FromResult<Template?>(null);

        var updated = new Template
        {
            Id = existing.Id,
            Name = request.Name ?? existing.Name,
            Description = request.Description ?? existing.Description,
            Channel = existing.Channel, // Channel cannot be changed
            Body = request.Body ?? existing.Body,
            ParameterNames = request.ParameterNames ?? existing.ParameterNames,
            Language = request.Language ?? existing.Language,
            Status = existing.Status,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _templates[id] = updated;
        _logger.LogInformation("Template {TemplateId} updated", id);

        return Task.FromResult<Template?>(updated);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string id, CancellationToken ct)
    {
        var removed = _templates.TryRemove(id, out _);
        if (removed)
            _logger.LogInformation("Template {TemplateId} deleted", id);
        return Task.FromResult(removed);
    }
}
