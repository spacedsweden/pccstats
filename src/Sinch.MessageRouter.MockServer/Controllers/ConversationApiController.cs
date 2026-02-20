using System.Diagnostics;
using Bogus;
using Microsoft.AspNetCore.Mvc;
using Sinch.MessageRouter.MockServer.Services;

namespace Sinch.MessageRouter.MockServer.Controllers;

/// <summary>
/// Mock implementation of the Sinch Conversation API v2 endpoints.
/// Generates realistic-looking responses with configurable latency and error rates.
/// </summary>
[ApiController]
public class ConversationApiController : ControllerBase
{
    private readonly LatencySimulator _latencySimulator;
    private readonly MockMetricsService _metrics;
    private readonly ILogger<ConversationApiController> _logger;
    private static readonly ThreadLocal<Faker> _faker = new(() => new Faker());

    // Per-channel cost simulation (USD per message)
    private static readonly Dictionary<string, double> ChannelCosts = new()
    {
        ["SMS"] = 0.0075,
        ["WHATSAPP"] = 0.005,
        ["RCS"] = 0.01,
        ["MESSENGER"] = 0.003,
        ["VIBER"] = 0.006,
        ["MMS"] = 0.02,
        ["TELEGRAM"] = 0.002,
        ["KAKAOTALK"] = 0.008,
        ["LINE"] = 0.007,
        ["INSTAGRAM"] = 0.004
    };

    public ConversationApiController(
        LatencySimulator latencySimulator,
        MockMetricsService metrics,
        ILogger<ConversationApiController> logger)
    {
        _latencySimulator = latencySimulator;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// POST /v2/projects/{projectId}/messages:send
    /// Mock send message - returns a message ID and accepted time.
    /// </summary>
    [HttpPost("v2/projects/{projectId}/messages:send")]
    public async Task<IActionResult> SendMessage(
        string projectId,
        [FromBody] SendMessageRequest? request)
    {
        var sw = Stopwatch.StartNew();
        _metrics.RecordRequest("messages:send");
        _metrics.RecordSendRequest();

        // Validate required fields
        if (request == null)
        {
            _metrics.RecordError();
            return BadRequest(new SinchError("bad_request", "Request body is required."));
        }
        if (string.IsNullOrWhiteSpace(request.app_id))
        {
            _metrics.RecordError();
            return BadRequest(new SinchError("bad_request", "app_id is required."));
        }
        if (request.recipient == null || string.IsNullOrWhiteSpace(request.recipient.contact_id))
        {
            _metrics.RecordError();
            return BadRequest(new SinchError("bad_request", "recipient.contact_id is required."));
        }
        if (request.message == null)
        {
            _metrics.RecordError();
            return BadRequest(new SinchError("bad_request", "message is required."));
        }

        // Simulate latency
        await _latencySimulator.SimulateLatencyAsync();

        // Check for simulated failure
        if (_latencySimulator.ShouldFail())
        {
            _metrics.RecordError();
            sw.Stop();
            _metrics.RecordLatency(sw.Elapsed.TotalMilliseconds);
            return StatusCode(503, new SinchError("service_unavailable",
                "The messaging service is temporarily unavailable. Please retry."));
        }

        var faker = _faker.Value!;
        string channel = request.message.channel ?? faker.PickRandom("SMS", "WHATSAPP", "RCS");
        double cost = ChannelCosts.GetValueOrDefault(channel.ToUpperInvariant(), 0.005);

        _metrics.RecordChannelUsage(channel.ToUpperInvariant());
        _metrics.RecordChannelCost(channel.ToUpperInvariant(), cost);

        sw.Stop();
        _metrics.RecordLatency(sw.Elapsed.TotalMilliseconds);

        return Ok(new
        {
            message_id = faker.Random.Guid().ToString(),
            accepted_time = DateTime.UtcNow.ToString("o"),
            channel,
            cost_usd = cost,
            status = "QUEUED",
            project_id = projectId,
            app_id = request.app_id,
            recipient = new
            {
                contact_id = request.recipient.contact_id
            }
        });
    }

    /// <summary>
    /// GET /v2/projects/{projectId}/messages
    /// Mock list messages with pagination.
    /// </summary>
    [HttpGet("v2/projects/{projectId}/messages")]
    public async Task<IActionResult> ListMessages(
        string projectId,
        [FromQuery] int page_size = 20,
        [FromQuery] string? page_token = null)
    {
        var sw = Stopwatch.StartNew();
        _metrics.RecordRequest("messages:list");
        _metrics.RecordGetRequest();

        await _latencySimulator.SimulateLatencyAsync();

        if (_latencySimulator.ShouldFail())
        {
            _metrics.RecordError();
            sw.Stop();
            _metrics.RecordLatency(sw.Elapsed.TotalMilliseconds);
            return StatusCode(503, new SinchError("service_unavailable",
                "The messaging service is temporarily unavailable."));
        }

        var faker = _faker.Value!;
        int count = Math.Min(page_size, 100);

        var messages = Enumerable.Range(0, count).Select(_ =>
        {
            string channel = faker.PickRandom("SMS", "WHATSAPP", "RCS", "MESSENGER", "VIBER");
            return new
            {
                message_id = faker.Random.Guid().ToString(),
                conversation_id = faker.Random.Guid().ToString(),
                contact_id = faker.Random.Guid().ToString(),
                channel_identity = new
                {
                    channel,
                    identity = faker.Phone.PhoneNumber("+1##########")
                },
                accept_time = DateTime.UtcNow.AddMinutes(-faker.Random.Int(1, 1440)).ToString("o"),
                direction = faker.PickRandom("TO_APP", "TO_CONTACT"),
                status = faker.PickRandom("DELIVERED", "READ", "SENT", "FAILED"),
                message = new
                {
                    text_message = new { text = faker.Lorem.Sentence() }
                },
                project_id = projectId
            };
        }).ToList();

        sw.Stop();
        _metrics.RecordLatency(sw.Elapsed.TotalMilliseconds);

        return Ok(new
        {
            messages,
            next_page_token = faker.Random.AlphaNumeric(32),
            total_size = faker.Random.Int(count, 10000)
        });
    }

    /// <summary>
    /// GET /v2/projects/{projectId}/messages/{messageId}
    /// Mock get single message by ID.
    /// </summary>
    [HttpGet("v2/projects/{projectId}/messages/{messageId}")]
    public async Task<IActionResult> GetMessage(string projectId, string messageId)
    {
        var sw = Stopwatch.StartNew();
        _metrics.RecordRequest("messages:get");
        _metrics.RecordGetRequest();

        await _latencySimulator.SimulateLatencyAsync();

        if (_latencySimulator.ShouldFail())
        {
            _metrics.RecordError();
            sw.Stop();
            _metrics.RecordLatency(sw.Elapsed.TotalMilliseconds);
            return StatusCode(503, new SinchError("service_unavailable",
                "The messaging service is temporarily unavailable."));
        }

        var faker = _faker.Value!;
        string channel = faker.PickRandom("SMS", "WHATSAPP", "RCS", "MESSENGER");

        sw.Stop();
        _metrics.RecordLatency(sw.Elapsed.TotalMilliseconds);

        return Ok(new
        {
            message_id = messageId,
            conversation_id = faker.Random.Guid().ToString(),
            contact_id = faker.Random.Guid().ToString(),
            channel_identity = new
            {
                channel,
                identity = faker.Phone.PhoneNumber("+1##########")
            },
            accept_time = DateTime.UtcNow.AddMinutes(-faker.Random.Int(1, 60)).ToString("o"),
            direction = faker.PickRandom("TO_APP", "TO_CONTACT"),
            status = faker.PickRandom("DELIVERED", "READ", "SENT"),
            message = new
            {
                text_message = new { text = faker.Lorem.Sentence() }
            },
            processing_mode = "CONVERSATION",
            project_id = projectId
        });
    }

    /// <summary>
    /// POST /v2/projects/{projectId}/webhooks
    /// Mock register a webhook.
    /// </summary>
    [HttpPost("v2/projects/{projectId}/webhooks")]
    public async Task<IActionResult> CreateWebhook(
        string projectId,
        [FromBody] CreateWebhookRequest? request)
    {
        var sw = Stopwatch.StartNew();
        _metrics.RecordRequest("webhooks:create");
        _metrics.RecordWebhookRequest();

        if (request == null)
        {
            _metrics.RecordError();
            return BadRequest(new SinchError("bad_request", "Request body is required."));
        }
        if (string.IsNullOrWhiteSpace(request.app_id))
        {
            _metrics.RecordError();
            return BadRequest(new SinchError("bad_request", "app_id is required."));
        }
        if (string.IsNullOrWhiteSpace(request.target))
        {
            _metrics.RecordError();
            return BadRequest(new SinchError("bad_request", "target URL is required."));
        }
        if (request.triggers == null || request.triggers.Length == 0)
        {
            _metrics.RecordError();
            return BadRequest(new SinchError("bad_request", "At least one trigger is required."));
        }

        await _latencySimulator.SimulateLatencyAsync();

        if (_latencySimulator.ShouldFail())
        {
            _metrics.RecordError();
            sw.Stop();
            _metrics.RecordLatency(sw.Elapsed.TotalMilliseconds);
            return StatusCode(503, new SinchError("service_unavailable",
                "The webhook service is temporarily unavailable."));
        }

        var faker = _faker.Value!;

        sw.Stop();
        _metrics.RecordLatency(sw.Elapsed.TotalMilliseconds);

        return Ok(new
        {
            webhook_id = faker.Random.Guid().ToString(),
            app_id = request.app_id,
            target = request.target,
            target_type = request.target_type ?? "HTTP",
            triggers = request.triggers,
            secret = request.secret ?? faker.Random.AlphaNumeric(32),
            create_time = DateTime.UtcNow.ToString("o"),
            update_time = DateTime.UtcNow.ToString("o"),
            project_id = projectId
        });
    }

    /// <summary>
    /// GET /v2/projects/{projectId}/apps/{appId}/webhooks
    /// Mock list webhooks for an app.
    /// </summary>
    [HttpGet("v2/projects/{projectId}/apps/{appId}/webhooks")]
    public async Task<IActionResult> ListWebhooks(string projectId, string appId)
    {
        var sw = Stopwatch.StartNew();
        _metrics.RecordRequest("webhooks:list");
        _metrics.RecordWebhookRequest();

        await _latencySimulator.SimulateLatencyAsync();

        if (_latencySimulator.ShouldFail())
        {
            _metrics.RecordError();
            sw.Stop();
            _metrics.RecordLatency(sw.Elapsed.TotalMilliseconds);
            return StatusCode(503, new SinchError("service_unavailable",
                "The webhook service is temporarily unavailable."));
        }

        var faker = _faker.Value!;
        string[] triggerOptions = { "MESSAGE_DELIVERY", "MESSAGE_INBOUND", "EVENT_INBOUND",
            "CONVERSATION_START", "CONVERSATION_STOP", "CONTACT_CREATE", "UNSUPPORTED" };

        var webhooks = Enumerable.Range(0, faker.Random.Int(1, 5)).Select(_ => new
        {
            webhook_id = faker.Random.Guid().ToString(),
            app_id = appId,
            target = faker.Internet.Url(),
            target_type = "HTTP",
            triggers = faker.PickRandom(triggerOptions, faker.Random.Int(1, 3)).ToArray(),
            secret = faker.Random.AlphaNumeric(32),
            create_time = DateTime.UtcNow.AddDays(-faker.Random.Int(1, 90)).ToString("o"),
            update_time = DateTime.UtcNow.AddHours(-faker.Random.Int(1, 24)).ToString("o"),
            project_id = projectId
        }).ToList();

        sw.Stop();
        _metrics.RecordLatency(sw.Elapsed.TotalMilliseconds);

        return Ok(new { webhooks });
    }

    /// <summary>
    /// POST /v2/projects/{projectId}/contacts
    /// Mock create a contact.
    /// </summary>
    [HttpPost("v2/projects/{projectId}/contacts")]
    public async Task<IActionResult> CreateContact(
        string projectId,
        [FromBody] CreateContactRequest? request)
    {
        var sw = Stopwatch.StartNew();
        _metrics.RecordRequest("contacts:create");
        _metrics.RecordContactRequest();

        if (request == null)
        {
            _metrics.RecordError();
            return BadRequest(new SinchError("bad_request", "Request body is required."));
        }
        if (request.channel_identities == null || request.channel_identities.Length == 0)
        {
            _metrics.RecordError();
            return BadRequest(new SinchError("bad_request",
                "At least one channel_identity is required."));
        }

        await _latencySimulator.SimulateLatencyAsync();

        if (_latencySimulator.ShouldFail())
        {
            _metrics.RecordError();
            sw.Stop();
            _metrics.RecordLatency(sw.Elapsed.TotalMilliseconds);
            return StatusCode(503, new SinchError("service_unavailable",
                "The contacts service is temporarily unavailable."));
        }

        var faker = _faker.Value!;

        sw.Stop();
        _metrics.RecordLatency(sw.Elapsed.TotalMilliseconds);

        return Ok(new
        {
            contact_id = faker.Random.Guid().ToString(),
            channel_identities = request.channel_identities,
            display_name = request.display_name ?? faker.Name.FullName(),
            email = request.email ?? faker.Internet.Email(),
            external_id = request.external_id ?? faker.Random.Guid().ToString(),
            language = request.language ?? "en-US",
            metadata = request.metadata ?? "",
            create_time = DateTime.UtcNow.ToString("o"),
            update_time = DateTime.UtcNow.ToString("o"),
            project_id = projectId
        });
    }
}

// --- Request/Response DTOs ---

public class SendMessageRequest
{
    public string? app_id { get; set; }
    public MessageRecipient? recipient { get; set; }
    public MessageBody? message { get; set; }
    public string? callback_url { get; set; }
    public Dictionary<string, object>? channel_properties { get; set; }
    public string? queue { get; set; }
}

public class MessageRecipient
{
    public string? contact_id { get; set; }
    public IdentifiedBy? identified_by { get; set; }
}

public class IdentifiedBy
{
    public ChannelIdentity[]? channel_identities { get; set; }
}

public class ChannelIdentity
{
    public string? channel { get; set; }
    public string? identity { get; set; }
}

public class MessageBody
{
    public TextMessage? text_message { get; set; }
    public MediaMessage? media_message { get; set; }
    public string? channel { get; set; }
}

public class TextMessage
{
    public string? text { get; set; }
}

public class MediaMessage
{
    public string? url { get; set; }
    public string? thumbnail_url { get; set; }
}

public class CreateWebhookRequest
{
    public string? app_id { get; set; }
    public string? target { get; set; }
    public string? target_type { get; set; }
    public string[]? triggers { get; set; }
    public string? secret { get; set; }
}

public class CreateContactRequest
{
    public ChannelIdentity[]? channel_identities { get; set; }
    public string? display_name { get; set; }
    public string? email { get; set; }
    public string? external_id { get; set; }
    public string? language { get; set; }
    public string? metadata { get; set; }
}

public record SinchError(string code, string message, string? details = null);
