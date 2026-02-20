using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Dispatch;

namespace Sinch.MessageRouter.Gateway.Services;

/// <summary>
/// Manages dispatch rules for multi-channel message routing strategies.
/// </summary>
public interface IDispatchService
{
    /// <summary>Creates a new dispatch rule.</summary>
    Task<DispatchRule> CreateAsync(CreateDispatchRuleRequest request, CancellationToken ct = default);

    /// <summary>Lists all dispatch rules.</summary>
    Task<PaginatedResponse<DispatchRule>> ListAsync(string? cursor, int pageSize, CancellationToken ct = default);

    /// <summary>Gets a dispatch rule by ID.</summary>
    Task<DispatchRule?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>Updates a dispatch rule.</summary>
    Task<DispatchRule?> UpdateAsync(string id, UpdateDispatchRuleRequest request, CancellationToken ct = default);

    /// <summary>Deletes a dispatch rule.</summary>
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}
