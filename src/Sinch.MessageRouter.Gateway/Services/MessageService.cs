using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Messages;

namespace Sinch.MessageRouter.Gateway.Services;

/// <summary>
/// Sends messages via the Sinch Conversation API with channel transformation,
/// dispatch/fallback logic, and OpenTelemetry metrics.
/// Uses an in-memory store for message tracking (replace with a database in production).
/// </summary>
public sealed class MessageService : IMessageService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<MessageService> _logger;

    // In-memory message store (replace with DB in production)
    private readonly ConcurrentDictionary<string, MessageResponse> _messages = new();

    // OpenTelemetry custom metrics
    private static readonly Meter Meter = new("Sinch.MessageRouter", "1.0.0");
    private static readonly Counter<long> MessagesSentCounter =
        Meter.CreateCounter<long>("messages_sent_total", description: "Total messages sent through the gateway");
    private static readonly Counter<long> MessagesReceivedCounter =
        Meter.CreateCounter<long>("messages_received_total", description: "Total inbound messages received");
    private static readonly Counter<long> DispatchAttemptsCounter =
        Meter.CreateCounter<long>("dispatch_attempts_total", description: "Total dispatch route attempts");
    private static readonly Counter<long> ChannelUsageCounter =
        Meter.CreateCounter<long>("channel_usage_total", description: "Message count per channel");
    private static readonly Histogram<double> SendLatency =
        Meter.CreateHistogram<double>("message_send_duration_seconds", description: "Message send latency in seconds");

    public MessageService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<MessageService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<MessageResponse> SendAsync(SendMessageRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        // Handle multi-channel dispatch if dispatch steps are provided
        if (request.Dispatch is { Count: > 0 })
        {
            return await SendWithDispatchAsync(request, ct);
        }

        var channel = request.EffectiveChannel;
        var content = request.EffectiveContent
            ?? throw new ArgumentException("Message must have a body, content, or dispatch chain.");

        var messageId = GenerateId();
        var sinchPayload = BuildSinchPayload(request, channel, content);

        var client = _httpClientFactory.CreateClient("SinchApi");
        var projectId = _config["Sinch:ProjectId"];

        try
        {
            var response = await client.PostAsJsonAsync(
                $"/conversation/v1/projects/{projectId}/messages:send",
                sinchPayload,
                ct);

            var status = response.IsSuccessStatusCode ? MessageStatus.Queued : MessageStatus.Failed;
            string? errorMessage = null;

            if (!response.IsSuccessStatusCode)
            {
                errorMessage = $"Sinch API returned {(int)response.StatusCode} {response.StatusCode}";
                try
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    if (!string.IsNullOrWhiteSpace(body))
                        errorMessage += $": {body}";
                }
                catch
                {
                    // Ignore read errors on error response body
                }
            }

            var msg = new MessageResponse
            {
                MessageId = messageId,
                To = request.To,
                From = request.From ?? GetDefaultSender(channel),
                Channel = channel,
                Status = status,
                Content = content,
                CreatedAt = DateTimeOffset.UtcNow,
                Metadata = request.Metadata,
                CorrelationId = request.CorrelationId,
                ErrorMessage = errorMessage
            };

            _messages[messageId] = msg;
            RecordMetrics(channel, status, sw.Elapsed.TotalSeconds);

            _logger.LogInformation(
                "Message {MessageId} sent via {Channel} to {To} - Status: {Status}",
                messageId, channel, request.To, status);

            return msg;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message via {Channel} to {To}", channel, request.To);

            var msg = new MessageResponse
            {
                MessageId = messageId,
                To = request.To,
                From = request.From ?? GetDefaultSender(channel),
                Channel = channel,
                Status = MessageStatus.Failed,
                Content = content,
                CreatedAt = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message,
                Metadata = request.Metadata,
                CorrelationId = request.CorrelationId
            };

            _messages[messageId] = msg;
            RecordMetrics(channel, MessageStatus.Failed, sw.Elapsed.TotalSeconds);

            return msg;
        }
    }

    /// <inheritdoc />
    public async Task<BatchMessageResponse> SendBatchAsync(BatchSendRequest request, CancellationToken ct)
    {
        var results = new List<MessageResponse>();
        var errors = new List<BatchError>();

        // Pattern 1: Individual message requests
        if (request.Messages is { Count: > 0 })
        {
            foreach (var msg in request.Messages)
            {
                try
                {
                    var result = await SendAsync(msg, ct);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    errors.Add(new BatchError
                    {
                        To = msg.To,
                        Code = "SEND_FAILED",
                        Message = ex.Message
                    });
                }
            }
        }
        // Pattern 2: Shared template sent to multiple recipients
        else if (request.To is { Count: > 0 })
        {
            foreach (var recipient in request.To)
            {
                try
                {
                    var singleRequest = new SendMessageRequest
                    {
                        To = recipient,
                        From = request.From,
                        Body = request.Body,
                        Channel = request.Channel,
                        Content = request.Content,
                        Priority = request.Priority,
                        CallbackUrl = request.CallbackUrl,
                        Metadata = request.Metadata,
                        TtlSeconds = request.TtlSeconds
                    };
                    var result = await SendAsync(singleRequest, ct);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    errors.Add(new BatchError
                    {
                        To = recipient,
                        Code = "SEND_FAILED",
                        Message = ex.Message
                    });
                }
            }
        }
        else
        {
            throw new ArgumentException("Batch request must contain either 'messages' or 'to' recipients.");
        }

        return new BatchMessageResponse
        {
            BatchId = GenerateId(),
            TotalRecipients = results.Count + errors.Count,
            Accepted = results.Count,
            Rejected = errors.Count,
            Messages = results,
            Errors = errors.Count > 0 ? errors : null
        };
    }

    /// <inheritdoc />
    public Task<PaginatedResponse<MessageResponse>> ListAsync(MessageListQuery query, CancellationToken ct)
    {
        var messages = _messages.Values.AsEnumerable();

        if (query.Status.HasValue)
            messages = messages.Where(m => m.Status == query.Status.Value);
        if (query.Channel.HasValue)
            messages = messages.Where(m => m.Channel == query.Channel.Value);
        if (query.To is not null)
            messages = messages.Where(m => m.To == query.To);
        if (query.From is not null)
            messages = messages.Where(m => m.From == query.From);
        if (query.After.HasValue)
            messages = messages.Where(m => m.CreatedAt > query.After.Value);
        if (query.Before.HasValue)
            messages = messages.Where(m => m.CreatedAt < query.Before.Value);

        // Cursor-based pagination: skip messages until we find the cursor
        var ordered = messages.OrderByDescending(m => m.CreatedAt).ToList();

        if (!string.IsNullOrEmpty(query.Cursor))
        {
            var cursorIndex = ordered.FindIndex(m => m.MessageId == query.Cursor);
            if (cursorIndex >= 0)
                ordered = ordered.Skip(cursorIndex + 1).ToList();
        }

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = ordered.Take(pageSize).ToList();
        var hasMore = ordered.Count > pageSize;

        return Task.FromResult(new PaginatedResponse<MessageResponse>
        {
            Items = page,
            NextCursor = hasMore && page.Count > 0 ? page[^1].MessageId : null,
            TotalCount = _messages.Count,
            PageSize = pageSize
        });
    }

    /// <inheritdoc />
    public Task<MessageResponse?> GetAsync(string messageId, CancellationToken ct)
    {
        _messages.TryGetValue(messageId, out var msg);
        return Task.FromResult(msg);
    }

    /// <inheritdoc />
    public Task<bool> RevokeAsync(string messageId, CancellationToken ct)
    {
        if (!_messages.TryGetValue(messageId, out var msg))
            return Task.FromResult(false);

        // Can only revoke messages that are still queued
        if (msg.Status != MessageStatus.Queued)
            return Task.FromResult(false);

        var revoked = msg with { Status = MessageStatus.Revoked, UpdatedAt = DateTimeOffset.UtcNow };
        _messages[messageId] = revoked;

        _logger.LogInformation("Message {MessageId} revoked", messageId);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Executes multi-channel dispatch with failover strategy.
    /// Tries each dispatch step in order until one succeeds.
    /// </summary>
    private async Task<MessageResponse> SendWithDispatchAsync(SendMessageRequest request, CancellationToken ct)
    {
        foreach (var step in request.Dispatch!)
        {
            DispatchAttemptsCounter.Add(1,
                new KeyValuePair<string, object?>("strategy", "failover"),
                new KeyValuePair<string, object?>("channel", step.Channel.ToString()));

            try
            {
                var stepRequest = new SendMessageRequest
                {
                    To = request.To,
                    From = step.From ?? request.From,
                    Body = step.Body ?? request.Body,
                    Channel = step.Channel,
                    Content = step.Content ?? request.Content,
                    CallbackUrl = request.CallbackUrl,
                    Metadata = request.Metadata,
                    Priority = request.Priority,
                    CorrelationId = request.CorrelationId,
                    TtlSeconds = step.TimeoutSeconds ?? request.TtlSeconds
                };

                var result = await SendAsync(stepRequest, ct);

                if (result.Status != MessageStatus.Failed)
                {
                    _logger.LogInformation(
                        "Dispatch succeeded via {Channel} for {To}",
                        step.Channel, request.To);
                    return result;
                }

                _logger.LogWarning(
                    "Dispatch step {Channel} failed for {To}, trying next route",
                    step.Channel, request.To);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Dispatch step {Channel} threw exception for {To}, trying next route",
                    step.Channel, request.To);
            }
        }

        // All routes exhausted
        _logger.LogError("All dispatch routes failed for {To}", request.To);

        return new MessageResponse
        {
            MessageId = GenerateId(),
            To = request.To,
            From = request.From ?? "unknown",
            Channel = request.Dispatch![0].Channel,
            Status = MessageStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow,
            ErrorMessage = "All dispatch routes failed.",
            Metadata = request.Metadata,
            CorrelationId = request.CorrelationId
        };
    }

    /// <summary>
    /// Transforms the gateway request into a Sinch Conversation API request body.
    /// </summary>
    private object BuildSinchPayload(SendMessageRequest request, MessageChannel channel, MessageContent content)
    {
        var sinchChannel = MapToSinchChannel(channel);

        var messageBody = content.Type switch
        {
            "text" => (object)new { text_message = new { text = content.Text } },
            "media" => new
            {
                media_message = new
                {
                    url = content.MediaUrl,
                    thumbnail_url = content.ThumbnailUrl,
                    filename = content.FileName
                }
            },
            "location" => new
            {
                location_message = new
                {
                    coordinates = new { latitude = content.Latitude, longitude = content.Longitude },
                    label = content.LocationLabel
                }
            },
            "template" => new
            {
                template_message = new
                {
                    template_id = content.TemplateId,
                    language_code = content.LanguageCode ?? "en",
                    parameters = content.TemplateParameters
                }
            },
            "card" => new
            {
                card_message = new
                {
                    title = content.Title,
                    description = content.Description,
                    media_message = content.MediaUrl != null ? new { url = content.MediaUrl } : null,
                    choices = content.Actions?.Select(a => new
                    {
                        type = a.Type.ToUpperInvariant(),
                        title = a.Title,
                        postback_data = a.Payload
                    }).ToArray()
                }
            },
            "carousel" => new
            {
                carousel_message = new
                {
                    cards = content.Cards?.Select(c => new
                    {
                        title = c.Title,
                        description = c.Description,
                        media_message = c.MediaUrl != null ? new { url = c.MediaUrl } : null
                    }).ToArray()
                }
            },
            "choices" => new
            {
                choice_message = new
                {
                    text_message = new { text = content.Text },
                    choices = content.Actions?.Select(a => new
                    {
                        type = a.Type.ToUpperInvariant(),
                        title = a.Title,
                        postback_data = a.Payload
                    }).ToArray()
                }
            },
            _ => new { text_message = new { text = content.Text ?? string.Empty } }
        };

        return new
        {
            app_id = _config["Sinch:AppId"],
            recipient = new
            {
                identified_by = new
                {
                    channel_identities = new[]
                    {
                        new { channel = sinchChannel, identity = request.To }
                    }
                }
            },
            message = messageBody,
            channel_priority_order = new[] { sinchChannel },
            processing_strategy = "DEFAULT",
            correlation_id = request.CorrelationId,
            ttl = request.TtlSeconds
        };
    }

    /// <summary>
    /// Maps the gateway MessageChannel enum to the Sinch API channel string.
    /// </summary>
    private static string MapToSinchChannel(MessageChannel channel) => channel switch
    {
        MessageChannel.Sms => "SMS",
        MessageChannel.Mms => "MMS",
        MessageChannel.Rcs => "RCS",
        MessageChannel.WhatsApp => "WHATSAPP",
        MessageChannel.Messenger => "MESSENGER",
        MessageChannel.Viber => "VIBER",
        MessageChannel.Telegram => "TELEGRAM",
        MessageChannel.Line => "LINE",
        MessageChannel.KakaoTalk => "KAKAOTALK",
        MessageChannel.Instagram => "INSTAGRAM",
        _ => "SMS"
    };

    /// <summary>
    /// Gets the default sender identity for a channel from configuration.
    /// </summary>
    private string GetDefaultSender(MessageChannel channel)
    {
        return _config[$"ChannelDefaults:{channel}:DefaultSender"] ?? "default";
    }

    /// <summary>
    /// Records OTel metrics for a sent message.
    /// </summary>
    private static void RecordMetrics(MessageChannel channel, MessageStatus status, double latencySeconds)
    {
        var channelTag = new KeyValuePair<string, object?>("channel", channel.ToString());
        var statusTag = new KeyValuePair<string, object?>("status", status.ToString());

        MessagesSentCounter.Add(1, channelTag, statusTag);
        ChannelUsageCounter.Add(1, channelTag);
        SendLatency.Record(latencySeconds, channelTag);
    }

    /// <summary>
    /// Generates a unique ID for messages and batches.
    /// </summary>
    private static string GenerateId() => Guid.NewGuid().ToString("N")[..24];
}
