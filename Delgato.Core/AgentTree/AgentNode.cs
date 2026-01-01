namespace Delgato.Core.AgentTree;

/// <summary>
/// Represents a node in the agent execution tree.
/// Supports N-ary tree structure for hierarchical agent spawning.
/// </summary>
public sealed class AgentNode
{
    private readonly List<AgentNode> _children = new();
    private readonly object _lock = new();

    /// <summary>
    /// Unique identifier for this node.
    /// </summary>
    public required Guid NodeId { get; init; }

    /// <summary>
    /// The agent ID associated with this node.
    /// </summary>
    public required AgentId AgentId { get; init; }

    /// <summary>
    /// Parent node ID (null for root).
    /// </summary>
    public Guid? ParentNodeId { get; init; }

    /// <summary>
    /// Reference to parent node.
    /// </summary>
    public AgentNode? Parent { get; internal set; }

    /// <summary>
    /// The agent definition for this node.
    /// </summary>
    public required AgentDefinition Definition { get; init; }

    /// <summary>
    /// Current state of the agent node.
    /// </summary>
    public AgentNodeState State { get; private set; } = AgentNodeState.Created;

    /// <summary>
    /// Depth in the tree (root = 0).
    /// </summary>
    public int Depth { get; init; }

    /// <summary>
    /// Execution metrics for this node.
    /// </summary>
    public AgentNodeMetrics Metrics { get; } = new();

    /// <summary>
    /// Child nodes.
    /// </summary>
    public IReadOnlyList<AgentNode> Children
    {
        get
        {
            lock (_lock)
            {
                return _children.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// When the node was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When execution started.
    /// </summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>
    /// When execution completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Result from agent execution.
    /// </summary>
    public string? Result { get; private set; }

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Custom metadata.
    /// </summary>
    public IDictionary<string, object> Metadata { get; } = new Dictionary<string, object>();

    /// <summary>
    /// Adds a child node.
    /// </summary>
    public void AddChild(AgentNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        lock (_lock)
        {
            child.Parent = this;
            _children.Add(child);
        }
    }

    /// <summary>
    /// Removes a child node.
    /// </summary>
    public bool RemoveChild(Guid nodeId)
    {
        lock (_lock)
        {
            var child = _children.FirstOrDefault(c => c.NodeId == nodeId);
            if (child != null)
            {
                child.Parent = null;
                return _children.Remove(child);
            }
            return false;
        }
    }

    /// <summary>
    /// Marks the node as running.
    /// </summary>
    public void Start()
    {
        State = AgentNodeState.Running;
        StartedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the node as completed with a result.
    /// </summary>
    public void Complete(string result)
    {
        State = AgentNodeState.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        Result = result;
    }

    /// <summary>
    /// Marks the node as failed with an error.
    /// </summary>
    public void Fail(string errorMessage)
    {
        State = AgentNodeState.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Marks the node as cancelled.
    /// </summary>
    public void Cancel()
    {
        State = AgentNodeState.Cancelled;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets all descendants (DFS traversal).
    /// </summary>
    public IEnumerable<AgentNode> GetDescendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var descendant in child.GetDescendants())
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Gets the path from root to this node.
    /// </summary>
    public IReadOnlyList<AgentNode> GetPathFromRoot()
    {
        var path = new List<AgentNode>();
        var current = this;
        while (current != null)
        {
            path.Insert(0, current);
            current = current.Parent;
        }
        return path.AsReadOnly();
    }

    /// <summary>
    /// Gets total token usage for this node and all descendants.
    /// </summary>
    public int GetTotalTokenUsage()
    {
        return Metrics.TokensUsed + Children.Sum(c => c.GetTotalTokenUsage());
    }

    /// <summary>
    /// Gets total cost for this node and all descendants.
    /// </summary>
    public decimal GetTotalCost()
    {
        return Metrics.Cost + Children.Sum(c => c.GetTotalCost());
    }
}

/// <summary>
/// State of an agent node.
/// </summary>
public enum AgentNodeState
{
    Created,
    Running,
    WaitingForChildren,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Metrics for an agent node.
/// </summary>
public sealed class AgentNodeMetrics
{
    public int TokensUsed { get; set; }
    public int ToolCallsExecuted { get; set; }
    public decimal Cost { get; set; }
    public TimeSpan? ExecutionDuration { get; set; }
    public int RetryCount { get; set; }
}
