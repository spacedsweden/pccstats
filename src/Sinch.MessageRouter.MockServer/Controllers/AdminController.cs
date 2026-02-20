using Microsoft.AspNetCore.Mvc;
using Sinch.MessageRouter.MockServer.Services;

namespace Sinch.MessageRouter.MockServer.Controllers;

/// <summary>
/// Admin API for controlling the mock server at runtime.
/// Allows dynamic configuration of latency, error rates, and viewing metrics.
/// </summary>
[ApiController]
[Route("admin")]
public class AdminController : ControllerBase
{
    private readonly MockMetricsService _metrics;
    private readonly LatencySimulator _latencySimulator;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        MockMetricsService metrics,
        LatencySimulator latencySimulator,
        ILogger<AdminController> logger)
    {
        _metrics = metrics;
        _latencySimulator = latencySimulator;
        _logger = logger;
    }

    /// <summary>
    /// GET /admin/stats
    /// Returns comprehensive metrics including total requests, latency percentiles,
    /// error counts, channel breakdown, and cost tracking.
    /// </summary>
    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        var stats = _metrics.GetFullStats();
        return Ok(stats);
    }

    /// <summary>
    /// POST /admin/config
    /// Update latency and error rate configuration at runtime.
    /// All fields are optional; only provided fields are updated.
    /// </summary>
    [HttpPost("config")]
    public IActionResult UpdateConfig([FromBody] UpdateConfigRequest? request)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        _latencySimulator.UpdateConfig(config =>
        {
            if (request.min_latency_ms.HasValue)
                config.MinLatencyMs = request.min_latency_ms.Value;
            if (request.max_latency_ms.HasValue)
                config.MaxLatencyMs = request.max_latency_ms.Value;
            if (request.mean_latency_ms.HasValue)
                config.MeanLatencyMs = request.mean_latency_ms.Value;
            if (request.std_dev_ms.HasValue)
                config.StdDevMs = request.std_dev_ms.Value;
            if (request.error_rate.HasValue)
            {
                if (request.error_rate.Value < 0 || request.error_rate.Value > 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(request.error_rate),
                        "Error rate must be between 0.0 and 1.0");
                }
                config.ErrorRate = request.error_rate.Value;
            }
            if (request.enabled.HasValue)
                config.Enabled = request.enabled.Value;
        });

        var current = _latencySimulator.Config;
        _logger.LogInformation(
            "Mock server config updated: latency={MinMs}-{MaxMs}ms (mean={MeanMs}ms), error_rate={ErrorRate}",
            current.MinLatencyMs, current.MaxLatencyMs, current.MeanLatencyMs, current.ErrorRate);

        return Ok(new
        {
            message = "Configuration updated successfully.",
            config = new
            {
                min_latency_ms = current.MinLatencyMs,
                max_latency_ms = current.MaxLatencyMs,
                mean_latency_ms = current.MeanLatencyMs,
                std_dev_ms = current.StdDevMs,
                error_rate = current.ErrorRate,
                enabled = current.Enabled
            }
        });
    }

    /// <summary>
    /// POST /admin/reset
    /// Reset all accumulated metrics. Configuration is preserved.
    /// </summary>
    [HttpPost("reset")]
    public IActionResult ResetStats()
    {
        _metrics.Reset();
        _logger.LogInformation("Mock server metrics reset.");
        return Ok(new
        {
            message = "All metrics have been reset.",
            reset_time = DateTime.UtcNow.ToString("o")
        });
    }

    /// <summary>
    /// GET /admin/health
    /// Health check endpoint returning server status and current configuration.
    /// </summary>
    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        var config = _latencySimulator.Config;
        return Ok(new
        {
            status = "healthy",
            service = "Sinch.MessageRouter.MockServer",
            timestamp = DateTime.UtcNow.ToString("o"),
            config = new
            {
                min_latency_ms = config.MinLatencyMs,
                max_latency_ms = config.MaxLatencyMs,
                mean_latency_ms = config.MeanLatencyMs,
                std_dev_ms = config.StdDevMs,
                error_rate = config.ErrorRate,
                latency_enabled = config.Enabled
            },
            total_requests_served = _metrics.TotalRequests
        });
    }

    /// <summary>
    /// GET /admin/config
    /// Retrieve the current configuration without modifying it.
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var config = _latencySimulator.Config;
        return Ok(new
        {
            min_latency_ms = config.MinLatencyMs,
            max_latency_ms = config.MaxLatencyMs,
            mean_latency_ms = config.MeanLatencyMs,
            std_dev_ms = config.StdDevMs,
            error_rate = config.ErrorRate,
            enabled = config.Enabled
        });
    }
}

public class UpdateConfigRequest
{
    public double? min_latency_ms { get; set; }
    public double? max_latency_ms { get; set; }
    public double? mean_latency_ms { get; set; }
    public double? std_dev_ms { get; set; }
    public double? error_rate { get; set; }
    public bool? enabled { get; set; }
}
