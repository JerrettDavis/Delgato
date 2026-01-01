namespace Delgato.Core.Governance;

/// <summary>
/// Service for recording and querying audit events.
/// Enterprise-grade audit trail for all system operations.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Records an audit event.
    /// </summary>
    Task RecordAsync(AuditEvent auditEvent, CancellationToken ct = default);

    /// <summary>
    /// Records multiple audit events.
    /// </summary>
    Task RecordBatchAsync(IEnumerable<AuditEvent> events, CancellationToken ct = default);

    /// <summary>
    /// Queries audit events with filters.
    /// </summary>
    Task<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken ct = default);

    /// <summary>
    /// Gets an audit event by ID.
    /// </summary>
    Task<AuditEvent?> GetByIdAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Gets all events for a correlation ID.
    /// </summary>
    Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdAsync(string correlationId, CancellationToken ct = default);

    /// <summary>
    /// Exports audit events to a specific format.
    /// </summary>
    Task<Stream> ExportAsync(AuditExportRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets audit statistics for a time range.
    /// </summary>
    Task<AuditStatistics> GetStatisticsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

/// <summary>
/// Query parameters for audit events.
/// </summary>
public sealed record AuditQuery
{
    public DateTimeOffset? FromTimestamp { get; init; }
    public DateTimeOffset? ToTimestamp { get; init; }
    public IReadOnlyList<string>? EventTypes { get; init; }
    public IReadOnlyList<AuditEventCategory>? Categories { get; init; }
    public IReadOnlyList<AuditEventSeverity>? Severities { get; init; }
    public string? ActorId { get; init; }
    public ActorType? ActorType { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public AuditOutcome? Outcome { get; init; }
    public string? CorrelationId { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public string? SearchText { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public AuditSortField SortBy { get; init; } = AuditSortField.Timestamp;
    public bool SortDescending { get; init; } = true;
}

/// <summary>
/// Fields for sorting audit queries.
/// </summary>
public enum AuditSortField
{
    Timestamp,
    EventType,
    Severity,
    ActorId,
    ResourceType,
    Outcome
}

/// <summary>
/// Result of an audit query.
/// </summary>
public sealed record AuditQueryResult
{
    public required IReadOnlyList<AuditEvent> Events { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
    public required int TotalPages { get; init; }
    public required bool HasNextPage { get; init; }
    public required bool HasPreviousPage { get; init; }
}

/// <summary>
/// Request for exporting audit events.
/// </summary>
public sealed record AuditExportRequest
{
    public required AuditQuery Query { get; init; }
    public required AuditExportFormat Format { get; init; }
    public bool IncludeProperties { get; init; } = true;
}

/// <summary>
/// Export format for audit events.
/// </summary>
public enum AuditExportFormat
{
    Json,
    Csv,
    Excel,
    Pdf
}

/// <summary>
/// Statistics for audit events.
/// </summary>
public sealed record AuditStatistics
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public required long TotalEvents { get; init; }
    public required IReadOnlyDictionary<string, long> EventsByType { get; init; }
    public required IReadOnlyDictionary<AuditEventCategory, long> EventsByCategory { get; init; }
    public required IReadOnlyDictionary<AuditEventSeverity, long> EventsBySeverity { get; init; }
    public required IReadOnlyDictionary<AuditOutcome, long> EventsByOutcome { get; init; }
    public required IReadOnlyDictionary<string, long> TopActors { get; init; }
    public required IReadOnlyDictionary<string, long> TopResources { get; init; }
    public required double SuccessRate { get; init; }
    public required TimeSpan AverageDuration { get; init; }
}
