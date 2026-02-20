using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sinch.MessageRouter.Gateway.Configuration;
using Sinch.MessageRouter.Gateway.Middleware;
using Sinch.MessageRouter.Gateway.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── JSON serialization ──────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// ─── YARP Reverse Proxy ──────────────────────────────────────────────
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddSinchTransforms();

// ─── HTTP Clients ────────────────────────────────────────────────────
builder.Services.AddHttpClient("SinchApi", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration.GetValue<string>("Sinch:BaseUrl") ?? "https://us.conversation.api.sinch.com");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("Sinch:TimeoutSeconds", 30));
})
.AddStandardResilienceHandler();

builder.Services.AddHttpClient(); // Default client for webhook delivery

// ─── Services ────────────────────────────────────────────────────────
// Register dispatch first since MessageService depends on it for routing rule resolution
builder.Services.AddSingleton<IDispatchService, DispatchService>();
builder.Services.AddSingleton<IMessageService, MessageService>();
builder.Services.AddSingleton<IWebhookService, WebhookService>();
builder.Services.AddSingleton<ITemplateService, TemplateService>();

// ─── Rate Limiting ───────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Per API key rate limit: 1000 requests per minute per key
    options.AddPolicy("PerApiKey", httpContext =>
    {
        var apiKey = httpContext.Request.Headers["X-API-Key"].FirstOrDefault()
            ?? httpContext.Request.Headers.Authorization.FirstOrDefault()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(apiKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:FixedWindow:PermitLimit", 1000),
            Window = TimeSpan.FromSeconds(
                builder.Configuration.GetValue("RateLimiting:FixedWindow:WindowSeconds", 60)),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = builder.Configuration.GetValue("RateLimiting:FixedWindow:QueueLimit", 50)
        });
    });

    // Global rate limit across all API keys
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter("global", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:GlobalLimit", 10000),
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 200
        }));
});

// ─── OpenTelemetry ───────────────────────────────────────────────────
var otelServiceName = builder.Configuration.GetValue("Otel:ServiceName", "sinch-message-router")!;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r
        .AddService(otelServiceName)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["service.version"] = "1.0.0"
        }))
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation();
        m.AddHttpClientInstrumentation();
        m.AddMeter("Sinch.MessageRouter");       // messages_sent_total, dispatch_attempts_total, channel_usage_total
        m.AddMeter("Sinch.MessageRouter.Webhooks"); // webhook_deliveries_total
        m.AddPrometheusExporter();
        m.AddOtlpExporter();
    })
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation();
        t.AddHttpClientInstrumentation();
        t.AddSource("Sinch.MessageRouter");
        t.AddOtlpExporter();
    });

// ─── Health Checks ───────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ─── CORS ────────────────────────────────────────────────────────────
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (corsOrigins is { Length: > 0 })
        {
            policy.WithOrigins(corsOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        }
    });
});

var app = builder.Build();

// ─── Middleware Pipeline ─────────────────────────────────────────────
// Order: Error handling -> CORS -> Rate limiting -> Auth -> Controllers/YARP
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<ApiKeyAuthMiddleware>();

// ─── Endpoints ───────────────────────────────────────────────────────
app.MapHealthChecks("/health");
app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");
app.MapControllers().RequireRateLimiting("PerApiKey");
app.MapReverseProxy();

app.MapGet("/", () => Results.Ok(new
{
    service = "Sinch MessageRouter Gateway",
    version = "1.0.0",
    status = "running"
}));

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
