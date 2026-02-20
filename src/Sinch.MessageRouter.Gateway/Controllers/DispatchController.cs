using Microsoft.AspNetCore.Mvc;
using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Dispatch;
using Sinch.MessageRouter.Gateway.Services;

namespace Sinch.MessageRouter.Gateway.Controllers;

/// <summary>
/// Manages saved routing rules for multi-channel message delivery.
/// Routing rules define strategies (failover, broadcast, round-robin, cost-optimized)
/// that can be referenced by ID when sending messages via the "routingRule" field.
/// </summary>
[ApiController]
[Route("v1/routing-rules")]
public class RoutingRulesController : ControllerBase
{
    private readonly IDispatchService _dispatchService;
    private readonly ILogger<RoutingRulesController> _logger;

    public RoutingRulesController(IDispatchService dispatchService, ILogger<RoutingRulesController> logger)
    {
        _dispatchService = dispatchService;
        _logger = logger;
    }

    /// <summary>
    /// Create a reusable routing rule with a strategy and channel routes.
    /// Reference it by ID in the "routingRule" field when sending messages.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RoutingRule), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateRoutingRuleRequest request, CancellationToken ct)
    {
        _logger.LogInformation(
            "Creating routing rule '{Name}' with strategy {Strategy}",
            request.Name, request.Settings.Strategy);

        var result = await _dispatchService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>
    /// List all routing rules with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<RoutingRule>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _dispatchService.ListAsync(cursor, Math.Clamp(pageSize, 1, 100), ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a routing rule by its ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RoutingRule), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var result = await _dispatchService.GetAsync(id, ct);
        if (result is null)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Routing rule '{id}' not found."
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Update a routing rule. Only provided fields are modified.
    /// </summary>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(RoutingRule), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateRoutingRuleRequest request, CancellationToken ct)
    {
        var result = await _dispatchService.UpdateAsync(id, request, ct);
        if (result is null)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Routing rule '{id}' not found."
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Delete a routing rule.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var deleted = await _dispatchService.DeleteAsync(id, ct);
        if (!deleted)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Routing rule '{id}' not found."
            });
        }
        return NoContent();
    }
}
