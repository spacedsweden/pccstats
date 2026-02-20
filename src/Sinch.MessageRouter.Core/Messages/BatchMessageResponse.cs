namespace Sinch.MessageRouter.Core.Messages;

/// <summary>
/// Response returned after sending a batch of messages.
/// </summary>
public sealed class BatchMessageResponse
{
    /// <summary>Unique batch identifier.</summary>
    public required string BatchId { get; init; }

    /// <summary>Total number of recipients in the batch.</summary>
    public int TotalRecipients { get; init; }

    /// <summary>Number of messages accepted for delivery.</summary>
    public int Accepted { get; init; }

    /// <summary>Number of messages rejected.</summary>
    public int Rejected { get; init; }

    /// <summary>Individual results for accepted messages.</summary>
    public required IReadOnlyList<MessageResponse> Messages { get; init; }

    /// <summary>Errors for rejected messages, if any.</summary>
    public IReadOnlyList<BatchError>? Errors { get; init; }
}

/// <summary>
/// Error details for a single message in a batch that was rejected.
/// </summary>
public sealed class BatchError
{
    /// <summary>Recipient that failed.</summary>
    public required string To { get; init; }

    /// <summary>Error code.</summary>
    public required string Code { get; init; }

    /// <summary>Error message.</summary>
    public required string Message { get; init; }
}
