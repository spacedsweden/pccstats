using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Dispatch;

namespace Sinch.MessageRouter.Gateway.Services;

/// <summary>
/// Manages routing rules for multi-channel message delivery strategies.
/// </summary>
public interface IDispatchService
{
    /// <summary>Creates a new routing rule.</summary>
    Task<RoutingRule> CreateAsync(CreateRoutingRuleRequest request, CancellationToken ct = default);

    /// <summary>Lists all routing rules.</summary>
    Task<PaginatedResponse<RoutingRule>> ListAsync(string? cursor, int pageSize, CancellationToken ct = default);

    /// <summary>Gets a routing rule by ID.</summary>
    Task<RoutingRule?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>Updates a routing rule.</summary>
    Task<RoutingRule?> UpdateAsync(string id, UpdateRoutingRuleRequest request, CancellationToken ct = default);

    /// <summary>Deletes a routing rule.</summary>
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}
