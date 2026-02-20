using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Sinch.MessageRouter.Core.Common;
using Sinch.MessageRouter.Core.Messages;
using Sinch.MessageRouter.Core.Webhooks;

namespace Sinch.MessageRouter.Gateway.Services;

/// <summary>
/// In-memory webhook registration manager. Supports CRUD operations, test delivery,
/// and is designed with an interface that can be backed by a database in production.
/// </summary>
public sealed class WebhookService : IWebhookService
{
    private readonly ConcurrentDictionary<string, WebhookRegistration> _webhooks = new();
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookService> _logger;
    private readonly IConfiguration _config;

    // OTel metrics for webhook delivery tracking
    private static readonly Meter Meter = new("Sinch.MessageRouter.Webhooks", "1.0.0");
    private static readonly Counter<long> WebhookDeliveries =
        Meter.CreateCounter<long>("webhook_deliveries_total", description: "Total webhook delivery attempts");
    private static readonly Histogram<double> WebhookLatency =
        Meter.CreateHistogram<double>("webhook_delivery_duration_seconds", description: "Webhook delivery latency");

    public WebhookService(
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookService> logger,
        IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _config = config;
    }

    /// <inheritdoc />
    public Task<WebhookRegistration> CreateAsync(CreateWebhookRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            throw new ArgumentException("Webhook URL is required.");

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && uri.Scheme != "http"))
            throw new ArgumentException("Webhook URL must be a valid HTTP or HTTPS URL.");

        var id = Guid.NewGuid().ToString("N")[..16];
        var registration = new WebhookRegistration
        {
            Id = id,
            Url = request.Url,
            Events = request.Events,
            Secret = request.Secret,
            Headers = request.Headers,
            Active = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _webhooks[id] = registration;
        _logger.LogInformation("Webhook {WebhookId} registered for URL {Url}", id, request.Url);

        return Task.FromResult(registration);
    }

    /// <inheritdoc />
    public Task<PaginatedResponse<WebhookRegistration>> ListAsync(string? cursor, int pageSize, CancellationToken ct)
    {
        var webhooks = _webhooks.Values
            .OrderByDescending(w => w.CreatedAt)
            .ToList();

        if (!string.IsNullOrEmpty(cursor))
        {
            var cursorIndex = webhooks.FindIndex(w => w.Id == cursor);
            if (cursorIndex >= 0)
                webhooks = webhooks.Skip(cursorIndex + 1).ToList();
        }

        pageSize = Math.Clamp(pageSize, 1, 100);
        var page = webhooks.Take(pageSize).ToList();
        var hasMore = webhooks.Count > pageSize;

        return Task.FromResult(new PaginatedResponse<WebhookRegistration>
        {
            Items = page,
            NextCursor = hasMore && page.Count > 0 ? page[^1].Id : null,
            TotalCount = _webhooks.Count,
            PageSize = pageSize
        });
    }

    /// <inheritdoc />
    public Task<WebhookRegistration?> GetAsync(string id, CancellationToken ct)
    {
        _webhooks.TryGetValue(id, out var webhook);
        return Task.FromResult(webhook);
    }

    /// <inheritdoc />
    public Task<WebhookRegistration?> UpdateAsync(string id, UpdateWebhookRequest request, CancellationToken ct)
    {
        if (!_webhooks.TryGetValue(id, out var existing))
            return Task.FromResult<WebhookRegistration?>(null);

        var updated = new WebhookRegistration
        {
            Id = existing.Id,
            Url = request.Url ?? existing.Url,
            Events = request.Events ?? existing.Events,
            Secret = request.Secret ?? existing.Secret,
            Headers = request.Headers ?? existing.Headers,
            Active = request.Active ?? existing.Active,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _webhooks[id] = updated;
        _logger.LogInformation("Webhook {WebhookId} updated", id);

        return Task.FromResult<WebhookRegistration?>(updated);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string id, CancellationToken ct)
    {
        var removed = _webhooks.TryRemove(id, out _);
        if (removed)
            _logger.LogInformation("Webhook {WebhookId} deleted", id);
        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public async Task<WebhookTestResult> TestAsync(string id, CancellationToken ct)
    {
        if (!_webhooks.TryGetValue(id, out var webhook))
            throw new KeyNotFoundException($"Webhook '{id}' not found.");

        if (!webhook.Active)
        {
            return new WebhookTestResult
            {
                Success = false,
                ErrorMessage = "Webhook is inactive. Activate it before testing."
            };
        }

        var testEvent = new
        {
            eventId = Guid.NewGuid().ToString("N")[..16],
            eventType = "webhook.test",
            timestamp = DateTimeOffset.UtcNow,
            data = new
            {
                message = "This is a test event from the Sinch MessageRouter Gateway."
            }
        };

        var sw = Stopwatch.StartNew();

        try
        {
            var client = _httpClientFactory.CreateClient();
            var timeoutSeconds = _config.GetValue("Webhooks:TimeoutSeconds", 30);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, webhook.Url);
            requestMessage.Content = new StringContent(
                JsonSerializer.Serialize(testEvent),
                Encoding.UTF8,
                "application/json");

            // Add custom headers
            if (webhook.Headers is not null)
            {
                foreach (var header in webhook.Headers)
                {
                    requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var response = await client.SendAsync(requestMessage, ct);
            sw.Stop();

            var body = await response.Content.ReadAsStringAsync(ct);

            WebhookDeliveries.Add(1,
                new KeyValuePair<string, object?>("status", response.IsSuccessStatusCode ? "success" : "failure"));
            WebhookLatency.Record(sw.Elapsed.TotalSeconds);

            return new WebhookTestResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ResponseBody = body.Length > 1024 ? body[..1024] + "..." : body,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            WebhookDeliveries.Add(1, new KeyValuePair<string, object?>("status", "error"));

            _logger.LogWarning(ex, "Webhook test delivery to {Url} failed", webhook.Url);

            return new WebhookTestResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
    }
}
