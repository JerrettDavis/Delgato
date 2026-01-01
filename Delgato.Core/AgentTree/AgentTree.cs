using System.Collections.Concurrent;

namespace Delgato.Core.AgentTree;

/// <summary>
/// Represents a complete agent execution tree.
/// Manages the hierarchical spawning and execution of agents.
/// </summary>
public sealed class AgentTree
{
    private readonly ConcurrentDictionary<Guid, AgentNode> _nodeIndex = new();
    private readonly object _lock = new();

    /// <summary>
    /// Unique identifier for this tree.
    /// </summary>
    public Guid TreeId { get; } = Guid.NewGuid();

    /// <summary>
    /// Correlation ID for tracing.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Root node of the tree.
    /// </summary>
    public AgentNode? Root { get; private set; }

    /// <summary>
    /// Current state of the tree execution.
    /// </summary>
    public AgentTreeState State { get; private set; } = AgentTreeState.Created;

    /// <summary>
    /// Maximum allowed depth.
    /// </summary>
    public int MaxDepth { get; init; } = 10;

    /// <summary>
    /// Maximum total nodes allowed.
    /// </summary>
    public int MaxNodes { get; init; } = 100;

    /// <summary>
    /// Budget constraints for the entire tree.
    /// </summary>
    public Budget? Budget { get; init; }

    /// <summary>
    /// When the tree was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When execution started.
    /// </summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>
    /// When execution completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Total number of nodes in the tree.
    /// </summary>
    public int NodeCount => _nodeIndex.Count;

    /// <summary>
    /// Sets the root node of the tree.
    /// </summary>
    public void SetRoot(AgentNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        lock (_lock)
        {
            if (Root != null)
                throw new InvalidOperationException("Root node already set.");

            Root = node;
            _nodeIndex[node.NodeId] = node;
        }
    }

    /// <summary>
    /// Spawns a child node under the specified parent.
    /// </summary>
    public SpawnResult SpawnChild(Guid parentNodeId, AgentNode child)
    {
        ArgumentNullException.ThrowIfNull(child);

        lock (_lock)
        {
            if (!_nodeIndex.TryGetValue(parentNodeId, out var parent))
            {
                return SpawnResult.Failed("Parent node not found.");
            }

            if (child.Depth >= MaxDepth)
            {
                return SpawnResult.Failed($"Maximum depth ({MaxDepth}) exceeded.");
            }

            if (_nodeIndex.Count >= MaxNodes)
            {
                return SpawnResult.Failed($"Maximum nodes ({MaxNodes}) exceeded.");
            }

            parent.AddChild(child);
            _nodeIndex[child.NodeId] = child;

            return SpawnResult.Success(child);
        }
    }

    /// <summary>
    /// Gets a node by its ID.
    /// </summary>
    public AgentNode? GetNode(Guid nodeId)
    {
        _nodeIndex.TryGetValue(nodeId, out var node);
        return node;
    }

    /// <summary>
    /// Gets all nodes in the tree.
    /// </summary>
    public IReadOnlyList<AgentNode> GetAllNodes()
    {
        return _nodeIndex.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets nodes at a specific depth.
    /// </summary>
    public IReadOnlyList<AgentNode> GetNodesAtDepth(int depth)
    {
        return _nodeIndex.Values.Where(n => n.Depth == depth).ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets nodes in a specific state.
    /// </summary>
    public IReadOnlyList<AgentNode> GetNodesByState(AgentNodeState state)
    {
        return _nodeIndex.Values.Where(n => n.State == state).ToList().AsReadOnly();
    }

    /// <summary>
    /// Marks the tree as started.
    /// </summary>
    public void Start()
    {
        State = AgentTreeState.Running;
        StartedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the tree as completed.
    /// </summary>
    public void Complete()
    {
        State = AgentTreeState.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the tree as failed.
    /// </summary>
    public void Fail()
    {
        State = AgentTreeState.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets tree execution summary.
    /// </summary>
    public AgentTreeSummary GetSummary()
    {
        var nodes = _nodeIndex.Values.ToList();
        return new AgentTreeSummary
        {
            TreeId = TreeId,
            CorrelationId = CorrelationId,
            State = State,
            NodeCount = nodes.Count,
            MaxDepthReached = nodes.Count > 0 ? nodes.Max(n => n.Depth) : 0,
            TotalTokensUsed = nodes.Sum(n => n.Metrics.TokensUsed),
            TotalToolCalls = nodes.Sum(n => n.Metrics.ToolCallsExecuted),
            TotalCost = nodes.Sum(n => n.Metrics.Cost),
            CompletedNodes = nodes.Count(n => n.State == AgentNodeState.Completed),
            FailedNodes = nodes.Count(n => n.State == AgentNodeState.Failed),
            CancelledNodes = nodes.Count(n => n.State == AgentNodeState.Cancelled),
            Duration = CompletedAt.HasValue && StartedAt.HasValue
                ? CompletedAt.Value - StartedAt.Value
                : null
        };
    }

    /// <summary>
    /// Prunes completed branches to free memory.
    /// </summary>
    public int PruneCompletedBranches()
    {
        int pruned = 0;
        lock (_lock)
        {
            var nodesToPrune = _nodeIndex.Values
                .Where(n => n.State == AgentNodeState.Completed && n.Children.Count == 0)
                .ToList();

            foreach (var node in nodesToPrune)
            {
                if (node == Root) continue; // Don't prune root

                node.Parent?.RemoveChild(node.NodeId);
                _nodeIndex.TryRemove(node.NodeId, out _);
                pruned++;
            }
        }
        return pruned;
    }

    /// <summary>
    /// Traverses the tree in BFS order.
    /// </summary>
    public IEnumerable<AgentNode> TraverseBfs()
    {
        if (Root == null) yield break;

        var queue = new Queue<AgentNode>();
        queue.Enqueue(Root);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            yield return node;

            foreach (var child in node.Children)
            {
                queue.Enqueue(child);
            }
        }
    }

    /// <summary>
    /// Traverses the tree in DFS order.
    /// </summary>
    public IEnumerable<AgentNode> TraverseDfs()
    {
        if (Root == null) yield break;

        var stack = new Stack<AgentNode>();
        stack.Push(Root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;

            foreach (var child in node.Children.Reverse())
            {
                stack.Push(child);
            }
        }
    }
}

/// <summary>
/// State of the agent tree.
/// </summary>
public enum AgentTreeState
{
    Created,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Result of a spawn operation.
/// </summary>
public sealed record SpawnResult
{
    public bool IsSuccess { get; init; }
    public AgentNode? Node { get; init; }
    public string? ErrorMessage { get; init; }

    public static SpawnResult Success(AgentNode node) => new() { IsSuccess = true, Node = node };
    public static SpawnResult Failed(string error) => new() { IsSuccess = false, ErrorMessage = error };
}

/// <summary>
/// Summary of tree execution.
/// </summary>
public sealed record AgentTreeSummary
{
    public required Guid TreeId { get; init; }
    public required string CorrelationId { get; init; }
    public required AgentTreeState State { get; init; }
    public required int NodeCount { get; init; }
    public required int MaxDepthReached { get; init; }
    public required int TotalTokensUsed { get; init; }
    public required int TotalToolCalls { get; init; }
    public required decimal TotalCost { get; init; }
    public required int CompletedNodes { get; init; }
    public required int FailedNodes { get; init; }
    public required int CancelledNodes { get; init; }
    public TimeSpan? Duration { get; init; }
}
