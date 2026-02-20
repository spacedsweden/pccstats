using Microsoft.AspNetCore.Mvc;
using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Messages;
using Sinch.MessageRouter.Gateway.Services;

namespace Sinch.MessageRouter.Gateway.Controllers;

/// <summary>
/// Handles message sending, retrieval, and revocation.
/// Provides a simplified interface over the Sinch Conversation API.
/// </summary>
[ApiController]
[Route("v1/messages")]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(IMessageService messageService, ILogger<MessagesController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    /// <summary>
    /// Send a message. Supports progressive complexity:
    ///   Level 1 (SMS):      { "to": "+1234567890", "body": "Hello" }
    ///   Level 2 (channel):  { "to": "+1234567890", "channel": "whatsapp", "body": "Hello" }
    ///   Level 3 (rich):     { "to": "+1234567890", "channel": "rcs", "content": { "type": "card", ... } }
    ///   Level 4 (routing):  { "to": "+1234567890", "routes": [{ "channel": "rcs", ... }, { "channel": "sms", ... }] }
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest request, CancellationToken ct)
    {
        if (request.Body is null && request.Content is null && request.Routes is null && request.RoutingRule is null)
        {
            return BadRequest(new ApiError
            {
                Code = "MISSING_CONTENT",
                Message = "Provide at least one of: body, content, routes, or routingRule."
            });
        }

        _logger.LogInformation(
            "Sending message to {To} via {Channel}",
            request.To,
            request.Routes is { Count: > 0 } ? "multi-channel" : request.EffectiveChannel.ToString());

        var response = await _messageService.SendAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { messageId = response.MessageId }, response);
    }

    /// <summary>
    /// Send a batch of messages. Supports two patterns:
    ///   Pattern 1: { "messages": [{ "to": "...", "body": "..." }, ...] }
    ///   Pattern 2: { "to": ["+1...", "+1..."], "body": "Hello", "channel": "sms" }
    /// </summary>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(BatchMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendBatch([FromBody] BatchSendRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Processing batch message request");
        var response = await _messageService.SendBatchAsync(request, ct);
        return Ok(response);
    }

    /// <summary>
    /// List messages with optional filtering and cursor-based pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<MessageResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] MessageStatus? status,
        [FromQuery] MessageChannel? channel,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] DateTimeOffset? after,
        [FromQuery] DateTimeOffset? before,
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new MessageListQuery
        {
            Status = status,
            Channel = channel,
            From = from,
            To = to,
            After = after,
            Before = before,
            Cursor = cursor,
            PageSize = Math.Clamp(pageSize, 1, 100)
        };

        var result = await _messageService.ListAsync(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a message by its ID.
    /// </summary>
    [HttpGet("{messageId}")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string messageId, CancellationToken ct)
    {
        var response = await _messageService.GetAsync(messageId, ct);
        if (response is null)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Message '{messageId}' not found."
            });
        }
        return Ok(response);
    }

    /// <summary>
    /// Revoke (cancel) a message. Only messages in Queued status can be revoked.
    /// </summary>
    [HttpDelete("{messageId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke(string messageId, CancellationToken ct)
    {
        var message = await _messageService.GetAsync(messageId, ct);
        if (message is null)
        {
            return NotFound(new ApiError
            {
                Code = "NOT_FOUND",
                Message = $"Message '{messageId}' not found."
            });
        }

        var revoked = await _messageService.RevokeAsync(messageId, ct);
        if (!revoked)
        {
            return Conflict(new ApiError
            {
                Code = "CANNOT_REVOKE",
                Message = $"Message '{messageId}' cannot be revoked. Only messages in 'Queued' status can be revoked."
            });
        }

        return NoContent();
    }
}
