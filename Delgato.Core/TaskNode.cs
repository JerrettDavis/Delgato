namespace Delgato.Core;

/// <summary>
/// Represents a unit of work within a plan.
/// </summary>
public sealed record TaskNode
{
    public required string TaskId { get; init; }
    public required string Description { get; init; }
    public AgentId? AssignedAgent { get; init; }
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
    public IDictionary<string, object> Parameters { get; init; } = new Dictionary<string, object>();
    public TaskStatus Status { get; init; } = TaskStatus.Pending;
    public object? Result { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

