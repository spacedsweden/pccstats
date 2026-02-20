using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Templates;

namespace Sinch.MessageRouter.Gateway.Services;

/// <summary>
/// Manages message templates for channel-specific content.
/// </summary>
public interface ITemplateService
{
    /// <summary>Creates a new template.</summary>
    Task<Template> CreateAsync(CreateTemplateRequest request, CancellationToken ct = default);

    /// <summary>Lists templates with pagination.</summary>
    Task<PaginatedResponse<Template>> ListAsync(string? cursor, int pageSize, CancellationToken ct = default);

    /// <summary>Gets a template by ID.</summary>
    Task<Template?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>Updates a template.</summary>
    Task<Template?> UpdateAsync(string id, UpdateTemplateRequest request, CancellationToken ct = default);

    /// <summary>Deletes a template.</summary>
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}
