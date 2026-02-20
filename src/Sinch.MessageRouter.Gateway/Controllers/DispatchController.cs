using Microsoft.AspNetCore.Mvc;
using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Dispatch;
using Sinch.MessageRouter.Gateway.Services;

namespace Sinch.MessageRouter.Gateway.Controllers;

/// <summary>
/// Manages dispatch rules for multi-channel message routing.
/// Dispatch rules define strategies (failover, broadcast, round-robin, cost-optimized)
/// that can be referenced when sending messages.
/// </summary>
[ApiController]
[Route("v1/dispatch")]
public class DispatchController : ControllerBase
{
    private readonly IDispatchService _dispatchService;
    private readonly ILogger<DispatchController> _logger;

    public DispatchController(IDispatchService dispatchService, ILogger<DispatchController> logger)
    {
        _dispatchService = dispatchService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new dispatch rule with a routing strategy and channel routes.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DispatchRule), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateDispatchRuleRequest request, CancellationToken ct)
    {
        _logger.LogInformation(
            "Creating dispatch rule '{Name}' with strategy {Strategy}",
            request.Name, request.Settings.Strategy);

        var result = await _dispatchService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>
    /// List all dispatch rules with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<DispatchRule>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _dispatchService.ListAsync(cursor, Math.Clamp(pageSize, 1, 100), ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a dispatch rule by its ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DispatchRule), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var result = await _dispatchService.GetAsync(id, ct);
        if (result is null)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Dispatch rule '{id}' not found."
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Update a dispatch rule. Only provided fields are modified.
    /// </summary>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(DispatchRule), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateDispatchRuleRequest request, CancellationToken ct)
    {
        var result = await _dispatchService.UpdateAsync(id, request, ct);
        if (result is null)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Dispatch rule '{id}' not found."
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Delete a dispatch rule.
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
                Message = $"Dispatch rule '{id}' not found."
            });
        }
        return NoContent();
    }
}
