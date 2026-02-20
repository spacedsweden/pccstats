using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

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

builder.Services.AddHttpClient();

// ─── Services ────────────────────────────────────────────────────────
builder.Services.AddSingleton<IMessageService, MessageService>();
builder.Services.AddSingleton<IWebhookService, WebhookService>();

// ─── Rate Limiting ───────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("PerApiKey", httpContext =>
    {
        var apiKey = httpContext.Request.Headers["X-API-Key"].FirstOrDefault()
            ?? httpContext.Request.Headers.Authorization.FirstOrDefault()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(apiKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:PerKeyLimit", 1000),
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 50
        });
    });

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
        m.AddMeter("Sinch.MessageRouter");
        m.AddMeter("Sinch.MessageRouter.Webhooks");
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
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// ─── Middleware Pipeline ─────────────────────────────────────────────
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

public partial class Program { }
