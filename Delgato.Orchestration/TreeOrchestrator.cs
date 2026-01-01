using System.Collections.Concurrent;
using Delgato.Core;
using Delgato.Core.Abstractions;
using Delgato.Core.AgentTree;
using Microsoft.Extensions.Logging;
using ExecutionContext = Delgato.Core.ExecutionContext;
using TaskStatus = Delgato.Core.TaskStatus;

namespace Delgato.Orchestration;

/// <summary>
/// N-tree based orchestrator for hierarchical agent execution.
/// Supports spawning local, remote, and virtualized agents in a tree structure.
/// </summary>
public sealed class TreeOrchestrator : IOrchestrator
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IAgentFactory _agentFactory;
    private readonly IProviderRegistry _providerRegistry;
    private readonly ILogger<TreeOrchestrator> _logger;
    private readonly ConcurrentDictionary<Guid, AgentTree> _activeTrees = new();
    private readonly TreeOrchestratorOptions _options;

    public TreeOrchestrator(
        IAgentRegistry agentRegistry,
        IAgentFactory agentFactory,
        IProviderRegistry providerRegistry,
        ILogger<TreeOrchestrator> logger,
        TreeOrchestratorOptions? options = null)
    {
        _agentRegistry = agentRegistry ?? throw new ArgumentNullException(nameof(agentRegistry));
        _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
        _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new TreeOrchestratorOptions();
    }

    /// <summary>
    /// Gets all active execution trees.
    /// </summary>
    public IReadOnlyList<AgentTree> ActiveTrees => _activeTrees.Values.ToList().AsReadOnly();

    /// <inheritdoc/>
    public async ValueTask<Plan> CreatePlanAsync(
        RequestEnvelope request,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating execution tree for request {CorrelationId}",
            request.CorrelationId);

        // Create the agent tree
        var tree = new AgentTree
        {
            CorrelationId = request.CorrelationId,
            MaxDepth = _options.MaxTreeDepth,
            MaxNodes = _options.MaxTreeNodes,
            Budget = context.Budget
        };

        // Find the root orchestrator agent
        var orchestrator = await FindOrchestratorAsync(cancellationToken);
        if (orchestrator == null)
        {
            _logger.LogWarning("No orchestrator agent found, using first available agent");
            var allAgents = await _agentRegistry.GetAllDefinitionsAsync(cancellationToken);
            orchestrator = allAgents.FirstOrDefault();
        }

        if (orchestrator == null)
        {
            return new Plan
            {
                PlanId = Guid.NewGuid().ToString(),
                CorrelationId = request.CorrelationId,
                Description = "Failed to create plan - no agents available",
                Tasks = Array.Empty<TaskNode>(),
                Status = PlanStatus.Failed
            };
        }

        // Create root node
        var rootNode = new AgentNode
        {
            NodeId = Guid.NewGuid(),
            AgentId = orchestrator.Id,
            Definition = orchestrator,
            Depth = 0
        };

        tree.SetRoot(rootNode);
        _activeTrees[tree.TreeId] = tree;

        // Decompose the request into tasks
        var tasks = await DecomposeRequestAsync(request, orchestrator, context, cancellationToken);

        var plan = new Plan
        {
            PlanId = tree.TreeId.ToString(),
            CorrelationId = request.CorrelationId,
            Description = $"Tree execution plan with {tasks.Count} tasks",
            Tasks = tasks,
            Status = PlanStatus.Created,
            Depth = 0
        };

        _logger.LogInformation(
            "Created plan {PlanId} with {TaskCount} tasks for tree {TreeId}",
            plan.PlanId, tasks.Count, tree.TreeId);

        return plan;
    }

    /// <inheritdoc/>
    public async ValueTask<Plan> ExecutePlanAsync(
        Plan plan,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(plan.PlanId, out var treeId) || !_activeTrees.TryGetValue(treeId, out var tree))
        {
            _logger.LogWarning("Tree not found for plan {PlanId}", plan.PlanId);
            return plan with { Status = PlanStatus.Failed };
        }

        _logger.LogInformation(
            "Executing tree {TreeId} with {TaskCount} tasks",
            tree.TreeId, plan.Tasks.Count);

        tree.Start();
        var updatedTasks = new List<TaskNode>();
        var parallelTasks = new List<(TaskNode Task, Task<TaskNode> Execution)>();

        foreach (var task in plan.Tasks)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                updatedTasks.Add(task with { Status = TaskStatus.Cancelled });
                continue;
            }

            if (!context.IsWithinBudget())
            {
                _logger.LogWarning("Budget exceeded at task {TaskId}", task.TaskId);
                updatedTasks.Add(task with
                {
                    Status = TaskStatus.Cancelled,
                    ErrorMessage = "Budget exceeded"
                });
                continue;
            }

            // Check if task can run in parallel
            if (CanRunInParallel(task, plan.Tasks, updatedTasks))
            {
                var execution = ExecuteTaskInTreeAsync(task, tree, context, cancellationToken);
                parallelTasks.Add((task, execution.AsTask()));
            }
            else
            {
                // Wait for parallel tasks to complete first
                if (parallelTasks.Count > 0)
                {
                    var results = await Task.WhenAll(parallelTasks.Select(p => p.Execution));
                    updatedTasks.AddRange(results);
                    parallelTasks.Clear();
                }

                var result = await ExecuteTaskInTreeAsync(task, tree, context, cancellationToken);
                updatedTasks.Add(result);
            }
        }

        // Wait for remaining parallel tasks
        if (parallelTasks.Count > 0)
        {
            var results = await Task.WhenAll(parallelTasks.Select(p => p.Execution));
            updatedTasks.AddRange(results);
        }

        var finalStatus = DeterminePlanStatus(updatedTasks);

        if (finalStatus == PlanStatus.Completed || finalStatus == PlanStatus.Failed)
        {
            if (finalStatus == PlanStatus.Completed)
                tree.Complete();
            else
                tree.Fail();
        }

        var summary = tree.GetSummary();
        _logger.LogInformation(
            "Tree {TreeId} execution completed with status {Status}. " +
            "Nodes: {NodeCount}, Tokens: {Tokens}, Cost: {Cost}",
            tree.TreeId, finalStatus, summary.NodeCount,
            summary.TotalTokensUsed, summary.TotalCost);

        return plan with
        {
            Tasks = updatedTasks,
            Status = finalStatus,
            StartedAt = tree.StartedAt,
            CompletedAt = tree.CompletedAt
        };
    }

    /// <inheritdoc/>
    public async ValueTask<AgentId?> RouteRequestAsync(
        RequestEnvelope request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Routing request {CorrelationId}", request.CorrelationId);

        // Try to find by capability if specified
        if (request.Metadata.TryGetValue("required_capability", out var capabilityObj) &&
            capabilityObj is string capability)
        {
            var agents = await _agentRegistry.FindByCapabilityAsync(capability, cancellationToken);
            if (agents.Count > 0)
            {
                var agent = agents.First();
                _logger.LogInformation(
                    "Routing to agent {AgentId} with capability {Capability}",
                    agent.Id, capability);
                return agent.Id;
            }
        }

        // Prefer orchestrator
        var orchestrator = await FindOrchestratorAsync(cancellationToken);
        if (orchestrator != null)
        {
            _logger.LogInformation("Routing to orchestrator {AgentId}", orchestrator.Id);
            return orchestrator.Id;
        }

        // Fallback to first available
        var allAgents = await _agentRegistry.GetAllDefinitionsAsync(cancellationToken);
        var firstAgent = allAgents.FirstOrDefault();
        if (firstAgent != null)
        {
            _logger.LogInformation("Routing to fallback agent {AgentId}", firstAgent.Id);
            return firstAgent.Id;
        }

        _logger.LogWarning("No suitable agent found for request {CorrelationId}", request.CorrelationId);
        return null;
    }

    /// <summary>
    /// Spawns a child agent in an existing tree.
    /// </summary>
    public async ValueTask<SpawnResult> SpawnChildAgentAsync(
        Guid treeId,
        Guid parentNodeId,
        AgentId agentId,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_activeTrees.TryGetValue(treeId, out var tree))
        {
            return SpawnResult.Failed("Tree not found");
        }

        var parent = tree.GetNode(parentNodeId);
        if (parent == null)
        {
            return SpawnResult.Failed("Parent node not found");
        }

        var definition = await _agentRegistry.GetDefinitionAsync(agentId, cancellationToken);
        if (definition == null)
        {
            return SpawnResult.Failed($"Agent {agentId} not found");
        }

        var childNode = new AgentNode
        {
            NodeId = Guid.NewGuid(),
            AgentId = agentId,
            ParentNodeId = parentNodeId,
            Definition = definition,
            Depth = parent.Depth + 1
        };

        var result = tree.SpawnChild(parentNodeId, childNode);

        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "Spawned child agent {AgentId} at depth {Depth} in tree {TreeId}",
                agentId, childNode.Depth, treeId);
        }
        else
        {
            _logger.LogWarning(
                "Failed to spawn child agent {AgentId} in tree {TreeId}: {Error}",
                agentId, treeId, result.ErrorMessage);
        }

        return result;
    }

    /// <summary>
    /// Gets an active tree by ID.
    /// </summary>
    public AgentTree? GetTree(Guid treeId)
    {
        _activeTrees.TryGetValue(treeId, out var tree);
        return tree;
    }

    /// <summary>
    /// Gets summary of all active trees.
    /// </summary>
    public IReadOnlyList<AgentTreeSummary> GetTreeSummaries()
    {
        return _activeTrees.Values
            .Select(t => t.GetSummary())
            .ToList()
            .AsReadOnly();
    }

    private async Task<AgentDefinition?> FindOrchestratorAsync(CancellationToken ct)
    {
        var allAgents = await _agentRegistry.GetAllDefinitionsAsync(ct);
        return allAgents.FirstOrDefault(a => a.IsOrchestrator);
    }

    private async Task<IReadOnlyList<TaskNode>> DecomposeRequestAsync(
        RequestEnvelope request,
        AgentDefinition orchestrator,
        ExecutionContext context,
        CancellationToken ct)
    {
        // For simple requests, create a single task
        // In production, this would use LLM-based decomposition
        var tasks = new List<TaskNode>
        {
            new TaskNode
            {
                TaskId = $"task-{Guid.NewGuid():N}",
                Description = request.Payload?.ToString() ?? string.Empty,
                Status = TaskStatus.Pending,
                AssignedAgent = orchestrator.Id
            }
        };

        return tasks;
    }

    private async ValueTask<TaskNode> ExecuteTaskInTreeAsync(
        TaskNode task,
        AgentTree tree,
        ExecutionContext context,
        CancellationToken ct)
    {
        _logger.LogInformation("Executing task {TaskId} in tree {TreeId}", task.TaskId, tree.TreeId);

        var agentId = task.AssignedAgent;
        if (agentId == null)
        {
            return task with
            {
                Status = TaskStatus.Failed,
                ErrorMessage = "No agent assigned to task"
            };
        }

        var definition = await _agentRegistry.GetDefinitionAsync(agentId.Value, ct);
        if (definition == null)
        {
            return task with
            {
                Status = TaskStatus.Failed,
                ErrorMessage = $"Agent {agentId} not found"
            };
        }

        // Find or create node for this task
        var node = tree.GetAllNodes()
            .FirstOrDefault(n => n.AgentId == agentId.Value && n.State == AgentNodeState.Created);

        if (node == null)
        {
            // Create a new node under root
            node = new AgentNode
            {
                NodeId = Guid.NewGuid(),
                AgentId = agentId.Value,
                ParentNodeId = tree.Root?.NodeId,
                Definition = definition,
                Depth = 1
            };

            if (tree.Root != null)
            {
                tree.SpawnChild(tree.Root.NodeId, node);
            }
        }

        node.Start();

        try
        {
            var agent = await _agentFactory.CreateAgentAsync(definition, ct);
            context.CurrentDepth = node.Depth;

            var response = await agent.ExecuteAsync(task.Description, context, ct);

            node.Metrics.TokensUsed = response.TokensUsed;
            node.Metrics.ToolCallsExecuted = response.ToolCallsExecuted;
            context.TokensConsumed += response.TokensUsed;
            context.ToolCallsExecuted += response.ToolCallsExecuted;

            if (response.IsSuccess)
            {
                node.Complete(response.Content);
                return task with
                {
                    Status = TaskStatus.Completed,
                    Result = response.Content,
                    AssignedAgent = agentId
                };
            }
            else
            {
                node.Fail(response.ErrorMessage ?? "Unknown error");
                return task with
                {
                    Status = TaskStatus.Failed,
                    ErrorMessage = response.ErrorMessage,
                    AssignedAgent = agentId
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task {TaskId} execution failed", task.TaskId);
            node.Fail(ex.Message);
            return task with
            {
                Status = TaskStatus.Failed,
                ErrorMessage = ex.Message,
                AssignedAgent = agentId
            };
        }
    }

    private bool CanRunInParallel(TaskNode task, IReadOnlyList<TaskNode> allTasks, List<TaskNode> completed)
    {
        // Check if all dependencies are satisfied
        if (task.Dependencies == null || task.Dependencies.Count == 0)
            return true;

        return task.Dependencies.All(depId =>
            completed.Any(t => t.TaskId == depId && t.Status == TaskStatus.Completed));
    }

    private static PlanStatus DeterminePlanStatus(IReadOnlyList<TaskNode> tasks)
    {
        if (tasks.All(t => t.Status == TaskStatus.Completed))
            return PlanStatus.Completed;

        if (tasks.All(t => t.Status == TaskStatus.Failed || t.Status == TaskStatus.Cancelled))
            return PlanStatus.Failed;

        if (tasks.Any(t => t.Status == TaskStatus.Completed))
            return PlanStatus.PartiallyCompleted;

        if (tasks.Any(t => t.Status == TaskStatus.Cancelled))
            return PlanStatus.Cancelled;

        return PlanStatus.Running;
    }
}

/// <summary>
/// Options for the tree orchestrator.
/// </summary>
public sealed class TreeOrchestratorOptions
{
    /// <summary>
    /// Maximum depth of the agent tree.
    /// </summary>
    public int MaxTreeDepth { get; set; } = 10;

    /// <summary>
    /// Maximum number of nodes in the tree.
    /// </summary>
    public int MaxTreeNodes { get; set; } = 100;

    /// <summary>
    /// Enable parallel task execution.
    /// </summary>
    public bool EnableParallelExecution { get; set; } = true;

    /// <summary>
    /// Maximum parallel tasks.
    /// </summary>
    public int MaxParallelTasks { get; set; } = 5;

    /// <summary>
    /// Enable automatic tree pruning.
    /// </summary>
    public bool EnableAutoPruning { get; set; } = true;

    /// <summary>
    /// Prune completed branches after this duration.
    /// </summary>
    public TimeSpan PruneAfter { get; set; } = TimeSpan.FromMinutes(30);
}
