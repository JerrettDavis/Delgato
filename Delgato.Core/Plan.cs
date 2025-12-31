namespace Delgato.Core;

/// <summary>
/// Represents an execution plan as a directed acyclic graph (DAG) of tasks.
/// </summary>
public sealed record Plan
{
    public required string PlanId { get; init; }
    public required string CorrelationId { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<TaskNode> Tasks { get; init; } = Array.Empty<TaskNode>();
    public PlanStatus Status { get; init; } = PlanStatus.Created;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public int Depth { get; init; } = 0;
}

public enum PlanStatus
{
    Created,
    Running,
    Completed,
    PartiallyCompleted,
    Failed,
    Cancelled
}

