using Microsoft.AspNetCore.Mvc;
using Sinch.MessageRouter.Core.Messages;
using Sinch.MessageRouter.Gateway.Services;

namespace Sinch.MessageRouter.Gateway.Controllers;

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
    /// Send a message. Simplest: { "to": "+1234567890", "body": "Hello" }
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest request, CancellationToken ct)
    {
        if (request.Body is null && request.Content is null && request.Dispatch is null)
            return BadRequest(new ProblemDetails
            {
                Title = "Missing message content",
                Detail = "Provide at least one of: body, content, or dispatch.",
                Status = 400
            });

        var response = await _messageService.SendAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { messageId = response.MessageId }, response);
    }

    [HttpPost("batch")]
    [ProducesResponseType(typeof(BatchMessageResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> SendBatch([FromBody] BatchSendRequest request, CancellationToken ct)
    {
        var response = await _messageService.SendBatchAsync(request, ct);
        return AcceptedAtAction(nameof(List), response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(MessageListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] MessageStatus? status, [FromQuery] MessageChannel? channel,
        [FromQuery] string? to, [FromQuery] string? from,
        [FromQuery] DateTimeOffset? after, [FromQuery] DateTimeOffset? before,
        [FromQuery] string? cursor, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new MessageListQuery
        {
            Status = status, Channel = channel, To = to, From = from,
            After = after, Before = before, Cursor = cursor,
            PageSize = Math.Clamp(pageSize, 1, 100)
        };
        return Ok(await _messageService.ListAsync(query, ct));
    }

    [HttpGet("{messageId}")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string messageId, CancellationToken ct)
    {
        var response = await _messageService.GetAsync(messageId, ct);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpDelete("{messageId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(string messageId, CancellationToken ct)
    {
        return await _messageService.RevokeAsync(messageId, ct) ? NoContent() : NotFound();
    }
}
