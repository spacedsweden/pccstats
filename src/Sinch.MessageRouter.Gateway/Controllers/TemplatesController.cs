using Microsoft.AspNetCore.Mvc;
using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Templates;
using Sinch.MessageRouter.Gateway.Services;

namespace Sinch.MessageRouter.Gateway.Controllers;

/// <summary>
/// Manages message templates for channel-specific content.
/// Templates can be referenced when sending messages to ensure consistent formatting.
/// </summary>
[ApiController]
[Route("v1/templates")]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateService _templateService;
    private readonly ILogger<TemplatesController> _logger;

    public TemplatesController(ITemplateService templateService, ILogger<TemplatesController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new message template.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Template), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTemplateRequest request, CancellationToken ct)
    {
        _logger.LogInformation(
            "Creating template '{Name}' for channel {Channel}",
            request.Name, request.Channel);

        var result = await _templateService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>
    /// List templates with cursor-based pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<Template>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _templateService.ListAsync(cursor, Math.Clamp(pageSize, 1, 100), ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a template by its ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Template), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var result = await _templateService.GetAsync(id, ct);
        if (result is null)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Template '{id}' not found."
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Update a template. Only provided fields are modified.
    /// Channel cannot be changed after creation.
    /// </summary>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(Template), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateTemplateRequest request, CancellationToken ct)
    {
        var result = await _templateService.UpdateAsync(id, request, ct);
        if (result is null)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Template '{id}' not found."
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Delete a template.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var deleted = await _templateService.DeleteAsync(id, ct);
        if (!deleted)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Template '{id}' not found."
            });
        }
        return NoContent();
    }
}
