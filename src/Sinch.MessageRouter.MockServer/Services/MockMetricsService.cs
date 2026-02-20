using System.Collections.Concurrent;
using System.Diagnostics;

namespace Sinch.MessageRouter.MockServer.Services;

/// <summary>
/// Thread-safe metrics collection service for tracking request counts,
/// latency percentiles, and error rates across all mock endpoints.
/// Uses lock-free operations where possible for high-concurrency support.
/// </summary>
public class MockMetricsService
{
    private long _totalRequests;
    private long _totalErrors;
    private long _totalSendRequests;
    private long _totalGetRequests;
    private long _totalWebhookRequests;
    private long _totalContactRequests;
    private readonly ConcurrentDictionary<string, long> _endpointCounts = new();
    private readonly ConcurrentDictionary<string, long> _channelCounts = new();
    private readonly ConcurrentDictionary<string, double> _channelCosts = new();
    private readonly ConcurrentBag<double> _latencies = new();
    private readonly object _latencyLock = new();
    private DateTime _startTime = DateTime.UtcNow;

    // Reservoir sampling for latencies to avoid unbounded memory growth
    private const int MaxLatencySamples = 100_000;
    private double[] _latencyReservoir = new double[MaxLatencySamples];
    private long _latencySampleCount;
    private readonly Random _reservoirRandom = new();
    private readonly object _reservoirLock = new();

    public long TotalRequests => Interlocked.Read(ref _totalRequests);
    public long TotalErrors => Interlocked.Read(ref _totalErrors);
    public long TotalSendRequests => Interlocked.Read(ref _totalSendRequests);
    public long TotalGetRequests => Interlocked.Read(ref _totalGetRequests);
    public long TotalWebhookRequests => Interlocked.Read(ref _totalWebhookRequests);
    public long TotalContactRequests => Interlocked.Read(ref _totalContactRequests);
    public DateTime StartTime => _startTime;

    public void RecordRequest(string endpoint)
    {
        Interlocked.Increment(ref _totalRequests);
        _endpointCounts.AddOrUpdate(endpoint, 1, (_, count) => Interlocked.Increment(ref count));
    }

    public void RecordSendRequest()
    {
        Interlocked.Increment(ref _totalSendRequests);
    }

    public void RecordGetRequest()
    {
        Interlocked.Increment(ref _totalGetRequests);
    }

    public void RecordWebhookRequest()
    {
        Interlocked.Increment(ref _totalWebhookRequests);
    }

    public void RecordContactRequest()
    {
        Interlocked.Increment(ref _totalContactRequests);
    }

    public void RecordError()
    {
        Interlocked.Increment(ref _totalErrors);
    }

    public void RecordChannelUsage(string channel)
    {
        _channelCounts.AddOrUpdate(channel, 1, (_, count) => count + 1);
    }

    public void RecordChannelCost(string channel, double cost)
    {
        _channelCosts.AddOrUpdate(channel, cost, (_, existing) => existing + cost);
    }

    public void RecordLatency(double latencyMs)
    {
        long index = Interlocked.Increment(ref _latencySampleCount) - 1;

        if (index < MaxLatencySamples)
        {
            _latencyReservoir[index] = latencyMs;
        }
        else
        {
            // Reservoir sampling: replace a random element with decreasing probability
            lock (_reservoirLock)
            {
                int replaceIndex = _reservoirRandom.Next((int)Math.Min(index + 1, int.MaxValue));
                if (replaceIndex < MaxLatencySamples)
                {
                    _latencyReservoir[replaceIndex] = latencyMs;
                }
            }
        }
    }

    public LatencyPercentiles GetLatencyPercentiles()
    {
        long count = Math.Min(Interlocked.Read(ref _latencySampleCount), MaxLatencySamples);
        if (count == 0)
        {
            return new LatencyPercentiles(0, 0, 0, 0, 0);
        }

        var samples = new double[count];
        Array.Copy(_latencyReservoir, samples, count);
        Array.Sort(samples);

        return new LatencyPercentiles(
            Min: samples[0],
            P50: GetPercentile(samples, 0.50),
            P95: GetPercentile(samples, 0.95),
            P99: GetPercentile(samples, 0.99),
            Max: samples[count - 1]
        );
    }

    private static double GetPercentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 0) return 0;
        int index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Length - 1))];
    }

    public Dictionary<string, long> GetEndpointCounts()
    {
        return new Dictionary<string, long>(_endpointCounts);
    }

    public Dictionary<string, long> GetChannelCounts()
    {
        return new Dictionary<string, long>(_channelCounts);
    }

    public Dictionary<string, double> GetChannelCosts()
    {
        return new Dictionary<string, double>(_channelCosts);
    }

    public double GetErrorRate()
    {
        long total = Interlocked.Read(ref _totalRequests);
        if (total == 0) return 0;
        return (double)Interlocked.Read(ref _totalErrors) / total;
    }

    public double GetRequestsPerSecond()
    {
        long total = Interlocked.Read(ref _totalRequests);
        var elapsed = (DateTime.UtcNow - _startTime).TotalSeconds;
        if (elapsed <= 0) return 0;
        return total / elapsed;
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalRequests, 0);
        Interlocked.Exchange(ref _totalErrors, 0);
        Interlocked.Exchange(ref _totalSendRequests, 0);
        Interlocked.Exchange(ref _totalGetRequests, 0);
        Interlocked.Exchange(ref _totalWebhookRequests, 0);
        Interlocked.Exchange(ref _totalContactRequests, 0);
        Interlocked.Exchange(ref _latencySampleCount, 0);
        _latencyReservoir = new double[MaxLatencySamples];
        _endpointCounts.Clear();
        _channelCounts.Clear();
        _channelCosts.Clear();
        _startTime = DateTime.UtcNow;
    }

    public object GetFullStats()
    {
        var percentiles = GetLatencyPercentiles();
        return new
        {
            uptime_seconds = (DateTime.UtcNow - _startTime).TotalSeconds,
            total_requests = TotalRequests,
            total_errors = TotalErrors,
            error_rate = GetErrorRate(),
            requests_per_second = Math.Round(GetRequestsPerSecond(), 2),
            request_breakdown = new
            {
                send_messages = TotalSendRequests,
                get_requests = TotalGetRequests,
                webhook_requests = TotalWebhookRequests,
                contact_requests = TotalContactRequests
            },
            latency = new
            {
                min_ms = Math.Round(percentiles.Min, 2),
                p50_ms = Math.Round(percentiles.P50, 2),
                p95_ms = Math.Round(percentiles.P95, 2),
                p99_ms = Math.Round(percentiles.P99, 2),
                max_ms = Math.Round(percentiles.Max, 2)
            },
            endpoints = GetEndpointCounts(),
            channels = GetChannelCounts(),
            channel_costs = GetChannelCosts()
        };
    }
}

public record LatencyPercentiles(double Min, double P50, double P95, double P99, double Max);
