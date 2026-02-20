using System.Collections.Concurrent;
using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Dispatch;

namespace Sinch.MessageRouter.Gateway.Services;

/// <summary>
/// In-memory routing rule manager. Handles CRUD for reusable routing rules
/// that define multi-channel delivery strategies (failover, broadcast, round-robin, cost-optimized).
/// </summary>
public sealed class DispatchService : IDispatchService
{
    private readonly ConcurrentDictionary<string, RoutingRule> _rules = new();
    private readonly ILogger<DispatchService> _logger;

    public DispatchService(ILogger<DispatchService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<RoutingRule> CreateAsync(CreateRoutingRuleRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Routing rule name is required.");

        if (request.Settings.Routes is not { Count: > 0 })
            throw new ArgumentException("At least one channel route is required.");

        var id = Guid.NewGuid().ToString("N")[..16];
        var rule = new RoutingRule
        {
            Id = id,
            Name = request.Name,
            Description = request.Description,
            Settings = request.Settings,
            Active = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _rules[id] = rule;
        _logger.LogInformation(
            "Routing rule {RuleId} '{RuleName}' created with strategy {Strategy} and {RouteCount} routes",
            id, request.Name, request.Settings.Strategy, request.Settings.Routes.Count);

        return Task.FromResult(rule);
    }

    /// <inheritdoc />
    public Task<PaginatedResponse<RoutingRule>> ListAsync(string? cursor, int pageSize, CancellationToken ct)
    {
        var rules = _rules.Values
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        if (!string.IsNullOrEmpty(cursor))
        {
            var cursorIndex = rules.FindIndex(r => r.Id == cursor);
            if (cursorIndex >= 0)
                rules = rules.Skip(cursorIndex + 1).ToList();
        }

        pageSize = Math.Clamp(pageSize, 1, 100);
        var page = rules.Take(pageSize).ToList();
        var hasMore = rules.Count > pageSize;

        return Task.FromResult(new PaginatedResponse<RoutingRule>
        {
            Items = page,
            NextCursor = hasMore && page.Count > 0 ? page[^1].Id : null,
            TotalCount = _rules.Count,
            PageSize = pageSize
        });
    }

    /// <inheritdoc />
    public Task<RoutingRule?> GetAsync(string id, CancellationToken ct)
    {
        _rules.TryGetValue(id, out var rule);
        return Task.FromResult(rule);
    }

    /// <inheritdoc />
    public Task<RoutingRule?> UpdateAsync(string id, UpdateRoutingRuleRequest request, CancellationToken ct)
    {
        if (!_rules.TryGetValue(id, out var existing))
            return Task.FromResult<RoutingRule?>(null);

        var updated = new RoutingRule
        {
            Id = existing.Id,
            Name = request.Name ?? existing.Name,
            Description = request.Description ?? existing.Description,
            Settings = request.Settings ?? existing.Settings,
            Active = request.Active ?? existing.Active,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _rules[id] = updated;
        _logger.LogInformation("Routing rule {RuleId} updated", id);

        return Task.FromResult<RoutingRule?>(updated);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string id, CancellationToken ct)
    {
        var removed = _rules.TryRemove(id, out _);
        if (removed)
            _logger.LogInformation("Routing rule {RuleId} deleted", id);
        return Task.FromResult(removed);
    }
}
