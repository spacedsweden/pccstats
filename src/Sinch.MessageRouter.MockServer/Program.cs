using Sinch.MessageRouter.MockServer.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Configure services for high concurrency ---

// Register singleton services for thread-safe shared state
builder.Services.AddSingleton<MockMetricsService>();
builder.Services.AddSingleton<LatencySimulator>();

// Add controllers
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

// Configure Kestrel for high throughput
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = null; // unlimited
    options.Limits.MaxConcurrentUpgradedConnections = null;
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
});

// Increase thread pool for high concurrency
ThreadPool.SetMinThreads(200, 200);

var app = builder.Build();

// --- Apply environment variable configuration ---
var latencySimulator = app.Services.GetRequiredService<LatencySimulator>();

if (double.TryParse(Environment.GetEnvironmentVariable("MOCK_MIN_LATENCY_MS"), out var minLat))
    latencySimulator.UpdateConfig(c => c.MinLatencyMs = minLat);

if (double.TryParse(Environment.GetEnvironmentVariable("MOCK_MAX_LATENCY_MS"), out var maxLat))
    latencySimulator.UpdateConfig(c => c.MaxLatencyMs = maxLat);

if (double.TryParse(Environment.GetEnvironmentVariable("MOCK_MEAN_LATENCY_MS"), out var meanLat))
    latencySimulator.UpdateConfig(c => c.MeanLatencyMs = meanLat);

if (double.TryParse(Environment.GetEnvironmentVariable("MOCK_STD_DEV_MS"), out var stdDev))
    latencySimulator.UpdateConfig(c => c.StdDevMs = stdDev);

if (double.TryParse(Environment.GetEnvironmentVariable("MOCK_ERROR_RATE"), out var errorRate))
    latencySimulator.UpdateConfig(c => c.ErrorRate = errorRate);

if (bool.TryParse(Environment.GetEnvironmentVariable("MOCK_LATENCY_ENABLED"), out var latEnabled))
    latencySimulator.UpdateConfig(c => c.Enabled = latEnabled);

// --- Map routes ---
app.MapControllers();
app.MapHealthChecks("/health");

// Root info endpoint
app.MapGet("/", () => Results.Ok(new
{
    service = "Sinch.MessageRouter.MockServer",
    status = "running",
    version = "2.0.0",
    endpoints = new
    {
        send_message = "POST /v2/projects/{projectId}/messages:send",
        list_messages = "GET /v2/projects/{projectId}/messages",
        get_message = "GET /v2/projects/{projectId}/messages/{messageId}",
        create_webhook = "POST /v2/projects/{projectId}/webhooks",
        list_webhooks = "GET /v2/projects/{projectId}/apps/{appId}/webhooks",
        create_contact = "POST /v2/projects/{projectId}/contacts",
        admin_stats = "GET /admin/stats",
        admin_config = "POST /admin/config",
        admin_reset = "POST /admin/reset",
        admin_health = "GET /admin/health"
    }
}));

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var config = latencySimulator.Config;
logger.LogInformation(
    "Mock Sinch Conversation API starting. Latency: {Min}-{Max}ms (mean {Mean}ms), Error rate: {Rate}%",
    config.MinLatencyMs, config.MaxLatencyMs, config.MeanLatencyMs, config.ErrorRate * 100);

app.Run();
