using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Delgato.Core.Governance;
using Microsoft.Extensions.Logging;

namespace Delgato.Governance;

/// <summary>
/// In-memory implementation of the audit service.
/// Suitable for development and testing. For production, use a persistent store.
/// </summary>
public sealed class InMemoryAuditService : IAuditService
{
    private readonly ConcurrentDictionary<Guid, AuditEvent> _events = new();
    private readonly ConcurrentDictionary<string, List<Guid>> _correlationIndex = new();
    private readonly ILogger<InMemoryAuditService> _logger;
    private readonly object _lock = new();

    public InMemoryAuditService(ILogger<InMemoryAuditService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task RecordAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        _events[auditEvent.EventId] = auditEvent;

        // Index by correlation ID
        if (!string.IsNullOrEmpty(auditEvent.CorrelationId))
        {
            lock (_lock)
            {
                if (!_correlationIndex.TryGetValue(auditEvent.CorrelationId, out var list))
                {
                    list = new List<Guid>();
                    _correlationIndex[auditEvent.CorrelationId] = list;
                }
                list.Add(auditEvent.EventId);
            }
        }

        _logger.LogDebug(
            "Recorded audit event {EventId}: {EventType} - {Action} on {ResourceType}/{ResourceId}",
            auditEvent.EventId, auditEvent.EventType, auditEvent.Action,
            auditEvent.ResourceType, auditEvent.ResourceId);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task RecordBatchAsync(IEnumerable<AuditEvent> events, CancellationToken ct = default)
    {
        foreach (var evt in events)
        {
            await RecordAsync(evt, ct);
        }
    }

    /// <inheritdoc/>
    public Task<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        var allEvents = _events.Values.AsEnumerable();

        // Apply filters
        if (query.FromTimestamp.HasValue)
            allEvents = allEvents.Where(e => e.Timestamp >= query.FromTimestamp.Value);

        if (query.ToTimestamp.HasValue)
            allEvents = allEvents.Where(e => e.Timestamp <= query.ToTimestamp.Value);

        if (query.EventTypes?.Count > 0)
            allEvents = allEvents.Where(e => query.EventTypes.Contains(e.EventType));

        if (query.Categories?.Count > 0)
            allEvents = allEvents.Where(e => query.Categories.Contains(e.Category));

        if (query.Severities?.Count > 0)
            allEvents = allEvents.Where(e => query.Severities.Contains(e.Severity));

        if (!string.IsNullOrEmpty(query.ActorId))
            allEvents = allEvents.Where(e => e.ActorId == query.ActorId);

        if (query.ActorType.HasValue)
            allEvents = allEvents.Where(e => e.ActorType == query.ActorType.Value);

        if (!string.IsNullOrEmpty(query.ResourceType))
            allEvents = allEvents.Where(e => e.ResourceType == query.ResourceType);

        if (!string.IsNullOrEmpty(query.ResourceId))
            allEvents = allEvents.Where(e => e.ResourceId == query.ResourceId);

        if (query.Outcome.HasValue)
            allEvents = allEvents.Where(e => e.Outcome == query.Outcome.Value);

        if (!string.IsNullOrEmpty(query.CorrelationId))
            allEvents = allEvents.Where(e => e.CorrelationId == query.CorrelationId);

        if (query.Tags?.Count > 0)
            allEvents = allEvents.Where(e => query.Tags.Any(t => e.Tags.Contains(t)));

        if (!string.IsNullOrEmpty(query.SearchText))
        {
            var searchLower = query.SearchText.ToLowerInvariant();
            allEvents = allEvents.Where(e =>
                e.EventType.ToLowerInvariant().Contains(searchLower) ||
                e.Action.ToLowerInvariant().Contains(searchLower) ||
                e.ResourceType.ToLowerInvariant().Contains(searchLower) ||
                e.ResourceId.ToLowerInvariant().Contains(searchLower));
        }

        // Sort
        allEvents = query.SortBy switch
        {
            AuditSortField.EventType => query.SortDescending
                ? allEvents.OrderByDescending(e => e.EventType)
                : allEvents.OrderBy(e => e.EventType),
            AuditSortField.Severity => query.SortDescending
                ? allEvents.OrderByDescending(e => e.Severity)
                : allEvents.OrderBy(e => e.Severity),
            AuditSortField.ActorId => query.SortDescending
                ? allEvents.OrderByDescending(e => e.ActorId)
                : allEvents.OrderBy(e => e.ActorId),
            AuditSortField.ResourceType => query.SortDescending
                ? allEvents.OrderByDescending(e => e.ResourceType)
                : allEvents.OrderBy(e => e.ResourceType),
            AuditSortField.Outcome => query.SortDescending
                ? allEvents.OrderByDescending(e => e.Outcome)
                : allEvents.OrderBy(e => e.Outcome),
            _ => query.SortDescending
                ? allEvents.OrderByDescending(e => e.Timestamp)
                : allEvents.OrderBy(e => e.Timestamp)
        };

        var list = allEvents.ToList();
        var totalCount = list.Count;
        var totalPages = (int)Math.Ceiling((double)totalCount / query.PageSize);

        var pagedEvents = list
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var result = new AuditQueryResult
        {
            Events = pagedEvents,
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalPages = totalPages,
            HasNextPage = query.PageNumber < totalPages,
            HasPreviousPage = query.PageNumber > 1
        };

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<AuditEvent?> GetByIdAsync(Guid eventId, CancellationToken ct = default)
    {
        _events.TryGetValue(eventId, out var evt);
        return Task.FromResult(evt);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdAsync(string correlationId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_correlationIndex.TryGetValue(correlationId, out var eventIds))
            {
                var events = eventIds
                    .Select(id => _events.TryGetValue(id, out var e) ? e : null)
                    .Where(e => e != null)
                    .Cast<AuditEvent>()
                    .OrderBy(e => e.Timestamp)
                    .ToList();

                return Task.FromResult<IReadOnlyList<AuditEvent>>(events);
            }
        }

        return Task.FromResult<IReadOnlyList<AuditEvent>>(Array.Empty<AuditEvent>());
    }

    /// <inheritdoc/>
    public Task<Stream> ExportAsync(AuditExportRequest request, CancellationToken ct = default)
    {
        // Query events
        var queryResult = QueryAsync(request.Query with { PageSize = int.MaxValue }, ct).Result;

        Stream stream;

        switch (request.Format)
        {
            case AuditExportFormat.Json:
                var json = JsonSerializer.Serialize(queryResult.Events, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                break;

            case AuditExportFormat.Csv:
                var csv = new StringBuilder();
                csv.AppendLine("EventId,Timestamp,EventType,Category,Severity,ActorId,ResourceType,ResourceId,Action,Outcome,CorrelationId");
                foreach (var evt in queryResult.Events)
                {
                    csv.AppendLine($"{evt.EventId},{evt.Timestamp:O},{evt.EventType},{evt.Category},{evt.Severity},{evt.ActorId},{evt.ResourceType},{evt.ResourceId},{evt.Action},{evt.Outcome},{evt.CorrelationId}");
                }
                stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));
                break;

            default:
                throw new NotSupportedException($"Export format {request.Format} not supported");
        }

        return Task.FromResult(stream);
    }

    /// <inheritdoc/>
    public Task<AuditStatistics> GetStatisticsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var events = _events.Values
            .Where(e => e.Timestamp >= from && e.Timestamp <= to)
            .ToList();

        var stats = new AuditStatistics
        {
            From = from,
            To = to,
            TotalEvents = events.Count,
            EventsByType = events.GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => (long)g.Count()),
            EventsByCategory = events.GroupBy(e => e.Category)
                .ToDictionary(g => g.Key, g => (long)g.Count()),
            EventsBySeverity = events.GroupBy(e => e.Severity)
                .ToDictionary(g => g.Key, g => (long)g.Count()),
            EventsByOutcome = events.GroupBy(e => e.Outcome)
                .ToDictionary(g => g.Key, g => (long)g.Count()),
            TopActors = events.GroupBy(e => e.ActorId)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key, g => (long)g.Count()),
            TopResources = events.GroupBy(e => $"{e.ResourceType}/{e.ResourceId}")
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key, g => (long)g.Count()),
            SuccessRate = events.Count > 0
                ? (double)events.Count(e => e.Outcome == AuditOutcome.Success) / events.Count
                : 0,
            AverageDuration = events.Where(e => e.Duration.HasValue).Any()
                ? TimeSpan.FromTicks((long)events.Where(e => e.Duration.HasValue).Average(e => e.Duration!.Value.Ticks))
                : TimeSpan.Zero
        };

        return Task.FromResult(stats);
    }

    /// <summary>
    /// Clears all audit events. For testing purposes only.
    /// </summary>
    public void Clear()
    {
        _events.Clear();
        _correlationIndex.Clear();
    }

    /// <summary>
    /// Gets the total number of events.
    /// </summary>
    public int Count => _events.Count;
}
