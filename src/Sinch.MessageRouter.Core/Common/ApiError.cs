namespace Sinch.MessageRouter.Core.Common;

/// <summary>
/// Standard API error response.
/// </summary>
public sealed class ApiError
{
    /// <summary>
    /// Machine-readable error code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Optional detailed information about the error.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Correlation ID for tracing the error.
    /// </summary>
    public string? TraceId { get; init; }
}
