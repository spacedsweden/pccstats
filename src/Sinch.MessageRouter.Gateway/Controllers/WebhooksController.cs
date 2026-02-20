using Microsoft.AspNetCore.Mvc;
using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Webhooks;
using Sinch.MessageRouter.Gateway.Services;

namespace Sinch.MessageRouter.Gateway.Controllers;

/// <summary>
/// Manages webhook registrations for receiving event notifications
/// (message delivery, read receipts, failures, dispatch events).
/// </summary>
[ApiController]
[Route("v1/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookService _webhookService;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(IWebhookService webhookService, ILogger<WebhooksController> logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new webhook endpoint to receive event notifications.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(WebhookRegistration), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateWebhookRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Registering webhook for URL {Url}", request.Url);
        var webhook = await _webhookService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = webhook.Id }, webhook);
    }

    /// <summary>
    /// List all registered webhooks with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<WebhookRegistration>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _webhookService.ListAsync(cursor, Math.Clamp(pageSize, 1, 100), ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a webhook by its ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(WebhookRegistration), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var webhook = await _webhookService.GetAsync(id, ct);
        if (webhook is null)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Webhook '{id}' not found."
            });
        }
        return Ok(webhook);
    }

    /// <summary>
    /// Update a webhook registration. Only provided fields are modified.
    /// </summary>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(WebhookRegistration), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateWebhookRequest request, CancellationToken ct)
    {
        var webhook = await _webhookService.UpdateAsync(id, request, ct);
        if (webhook is null)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Webhook '{id}' not found."
            });
        }
        return Ok(webhook);
    }

    /// <summary>
    /// Delete a webhook registration.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var deleted = await _webhookService.DeleteAsync(id, ct);
        if (!deleted)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Webhook '{id}' not found."
            });
        }
        return NoContent();
    }

    /// <summary>
    /// Send a test event to the specified webhook to verify connectivity.
    /// </summary>
    [HttpPost("{id}/test")]
    [ProducesResponseType(typeof(WebhookTestResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Test(string id, CancellationToken ct)
    {
        _logger.LogInformation("Testing webhook {WebhookId}", id);
        var result = await _webhookService.TestAsync(id, ct);
        return Ok(result);
    }
}
