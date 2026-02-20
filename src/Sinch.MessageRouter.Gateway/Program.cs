using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// HTTP client with resilience
builder.Services.AddHttpClient("SinchApi", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration.GetValue<string>("Sinch:BaseUrl") ?? "https://us.conversation.api.sinch.com");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddStandardResilienceHandler();

// OpenTelemetry
var otelServiceName = builder.Configuration.GetValue("Otel:ServiceName", "sinch-message-router")!;
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(otelServiceName))
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation();
        m.AddHttpClientInstrumentation();
        m.AddMeter("Sinch.MessageRouter");
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

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");
app.MapReverseProxy();

app.MapGet("/", () => Results.Ok(new { service = "Sinch.MessageRouter.Gateway", status = "running" }));

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
