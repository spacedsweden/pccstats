using Microsoft.AspNetCore.Mvc;
using Sinch.MessageRouter.Core.Webhooks;
using Sinch.MessageRouter.Gateway.Services;

namespace Sinch.MessageRouter.Gateway.Controllers;

[ApiController]
[Route("v1/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookService _webhookService;

    public WebhooksController(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(WebhookRegistration), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateWebhookRequest request, CancellationToken ct)
    {
        var webhook = await _webhookService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { webhookId = webhook.Id }, webhook);
    }

    [HttpGet]
    [ProducesResponseType(typeof(WebhookListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _webhookService.ListAsync(ct));

    [HttpGet("{webhookId}")]
    [ProducesResponseType(typeof(WebhookRegistration), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string webhookId, CancellationToken ct)
    {
        var webhook = await _webhookService.GetAsync(webhookId, ct);
        return webhook is null ? NotFound() : Ok(webhook);
    }

    [HttpPatch("{webhookId}")]
    public async Task<IActionResult> Update(string webhookId, [FromBody] UpdateWebhookRequest request, CancellationToken ct)
    {
        var webhook = await _webhookService.UpdateAsync(webhookId, request, ct);
        return webhook is null ? NotFound() : Ok(webhook);
    }

    [HttpDelete("{webhookId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string webhookId, CancellationToken ct)
        => await _webhookService.DeleteAsync(webhookId, ct) ? NoContent() : NotFound();

    [HttpPost("{webhookId}/test")]
    public async Task<IActionResult> Test(string webhookId, CancellationToken ct)
    {
        var result = await _webhookService.TestAsync(webhookId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("inbound")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> InboundWebhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        await _webhookService.ProcessInboundAsync(payload, ct);
        return Ok();
    }
}
