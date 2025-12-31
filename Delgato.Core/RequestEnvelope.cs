namespace Delgato.Core;

/// <summary>
/// Envelope wrapping an incoming request from any transport.
/// </summary>
public sealed record RequestEnvelope
{
    /// <summary>
    /// Unique identifier for request correlation and tracing.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Source transport identifier (e.g., "http", "mcp", "filesystem", "servicebus").
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// The actual request payload.
    /// </summary>
    public required object Payload { get; init; }

    /// <summary>
    /// Additional metadata about the request (headers, user context, etc.).
    /// </summary>
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Timestamp when request was received.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
}

