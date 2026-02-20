namespace Sinch.MessageRouter.MockServer.Services;

/// <summary>
/// Configurable latency simulation service that adds realistic delays
/// using a normal distribution around a configurable mean.
/// Thread-safe for concurrent access from multiple request handlers.
/// </summary>
public class LatencySimulator
{
    private volatile LatencyConfig _config;
    private readonly ThreadLocal<Random> _random = new(() => new Random(Guid.NewGuid().GetHashCode()));

    public LatencySimulator()
    {
        _config = new LatencyConfig();
    }

    public LatencyConfig Config => _config;

    public void UpdateConfig(LatencyConfig newConfig)
    {
        _config = newConfig;
    }

    public void UpdateConfig(Action<LatencyConfig> modifier)
    {
        var config = new LatencyConfig
        {
            MinLatencyMs = _config.MinLatencyMs,
            MaxLatencyMs = _config.MaxLatencyMs,
            MeanLatencyMs = _config.MeanLatencyMs,
            StdDevMs = _config.StdDevMs,
            ErrorRate = _config.ErrorRate,
            Enabled = _config.Enabled
        };
        modifier(config);
        _config = config;
    }

    /// <summary>
    /// Simulates latency by awaiting a delay sampled from a normal distribution.
    /// Returns the actual delay in milliseconds.
    /// </summary>
    public async Task<double> SimulateLatencyAsync()
    {
        if (!_config.Enabled) return 0;

        double latencyMs = SampleLatency();
        if (latencyMs > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(latencyMs));
        }
        return latencyMs;
    }

    /// <summary>
    /// Determines whether this request should fail based on the configured error rate.
    /// </summary>
    public bool ShouldFail()
    {
        var random = _random.Value!;
        return random.NextDouble() < _config.ErrorRate;
    }

    /// <summary>
    /// Samples a latency value from a normal distribution, clamped to [min, max].
    /// Uses the Box-Muller transform for normal distribution sampling.
    /// </summary>
    private double SampleLatency()
    {
        var random = _random.Value!;
        double mean = _config.MeanLatencyMs;
        double stdDev = _config.StdDevMs;

        // Box-Muller transform for normal distribution
        double u1 = 1.0 - random.NextDouble(); // uniform(0,1] to avoid log(0)
        double u2 = random.NextDouble();
        double normalSample = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

        double latency = mean + stdDev * normalSample;

        // Clamp to configured bounds
        return Math.Max(_config.MinLatencyMs, Math.Min(_config.MaxLatencyMs, latency));
    }
}

/// <summary>
/// Configuration for latency simulation and error injection.
/// </summary>
public class LatencyConfig
{
    /// <summary>Minimum latency in milliseconds (default 50ms).</summary>
    public double MinLatencyMs { get; set; } = 50;

    /// <summary>Maximum latency in milliseconds (default 200ms).</summary>
    public double MaxLatencyMs { get; set; } = 200;

    /// <summary>Mean latency in milliseconds for the normal distribution (default 100ms).</summary>
    public double MeanLatencyMs { get; set; } = 100;

    /// <summary>Standard deviation in milliseconds for the normal distribution (default 30ms).</summary>
    public double StdDevMs { get; set; } = 30;

    /// <summary>Fraction of requests that should fail (0.0 to 1.0, default 0.01 = 1%).</summary>
    public double ErrorRate { get; set; } = 0.01;

    /// <summary>Whether latency simulation is enabled (default true).</summary>
    public bool Enabled { get; set; } = true;
}
