namespace Delgato.Core.Governance;

/// <summary>
/// Represents an audit event in the system.
/// All operations are tracked for enterprise compliance and debugging.
/// </summary>
public sealed record AuditEvent
{
    /// <summary>
    /// Unique identifier for this event.
    /// </summary>
    public required Guid EventId { get; init; }

    /// <summary>
    /// When the event occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Type of event (e.g., "AgentExecution", "ToolCall", "SpawnAgent").
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Category of the event for grouping.
    /// </summary>
    public required AuditEventCategory Category { get; init; }

    /// <summary>
    /// Severity level of the event.
    /// </summary>
    public required AuditEventSeverity Severity { get; init; }

    /// <summary>
    /// Actor who triggered the event (user, agent, system).
    /// </summary>
    public required string ActorId { get; init; }

    /// <summary>
    /// Type of actor.
    /// </summary>
    public required ActorType ActorType { get; init; }

    /// <summary>
    /// Type of resource involved.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    /// ID of the resource involved.
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    /// Action performed.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Outcome of the action.
    /// </summary>
    public required AuditOutcome Outcome { get; init; }

    /// <summary>
    /// Correlation ID for request tracing.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Parent event ID for hierarchical events.
    /// </summary>
    public Guid? ParentEventId { get; init; }

    /// <summary>
    /// Duration of the operation if applicable.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Additional properties for the event.
    /// </summary>
    public IReadOnlyDictionary<string, object> Properties { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    /// Tags for filtering and categorization.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// IP address of the client if applicable.
    /// </summary>
    public string? ClientIpAddress { get; init; }

    /// <summary>
    /// User agent string if applicable.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// Error details if the outcome was a failure.
    /// </summary>
    public AuditErrorDetails? Error { get; init; }
}

/// <summary>
/// Category of audit events.
/// </summary>
public enum AuditEventCategory
{
    Security,
    Agent,
    Tool,
    Orchestration,
    Configuration,
    System,
    Performance,
    Compliance
}

/// <summary>
/// Severity of audit events.
/// </summary>
public enum AuditEventSeverity
{
    Debug,
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Type of actor.
/// </summary>
public enum ActorType
{
    User,
    Agent,
    System,
    Service,
    External
}

/// <summary>
/// Outcome of an audited action.
/// </summary>
public enum AuditOutcome
{
    Success,
    Failure,
    Denied,
    Timeout,
    Cancelled,
    PartialSuccess
}

/// <summary>
/// Error details for failed operations.
/// </summary>
public sealed record AuditErrorDetails
{
    public required string ErrorCode { get; init; }
    public required string Message { get; init; }
    public string? StackTrace { get; init; }
    public string? InnerError { get; init; }
}
