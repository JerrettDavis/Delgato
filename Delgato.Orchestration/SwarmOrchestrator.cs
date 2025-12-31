using Delgato.Core;
using Delgato.Core.Abstractions;
using Microsoft.Extensions.Logging;
using ExecutionContext = Delgato.Core.ExecutionContext;
using TaskStatus = Delgato.Core.TaskStatus;

namespace Delgato.Orchestration;

/// <summary>
/// Core orchestrator implementation for planning, routing, and execution coordination.
/// </summary>
public sealed class SwarmOrchestrator : IOrchestrator
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly IAgentFactory _agentFactory;
    private readonly ILogger<SwarmOrchestrator> _logger;

    public SwarmOrchestrator(
        IAgentRegistry agentRegistry,
        IAgentFactory agentFactory,
        ILogger<SwarmOrchestrator> logger)
    {
        _agentRegistry = agentRegistry ?? throw new ArgumentNullException(nameof(agentRegistry));
        _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<Plan> CreatePlanAsync(
        RequestEnvelope request,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating plan for request {CorrelationId} from {Source}", 
            request.CorrelationId, request.Source);

        // For MVP, create a simple sequential plan
        // In v1, this would use SK planner or LLM-based decomposition
        var planId = Guid.NewGuid().ToString();
        
        var tasks = new List<TaskNode>
        {
            new()
            {
                TaskId = $"{planId}-task-1",
                Description = $"Process request: {request.Payload}",
                Status = TaskStatus.Pending
            }
        };

        var plan = new Plan
        {
            PlanId = planId,
            CorrelationId = request.CorrelationId,
            Description = $"Plan for {request.Source} request",
            Tasks = tasks,
            Status = PlanStatus.Created,
            Depth = context.CurrentDepth
        };

        return plan;
    }

    public async ValueTask<Plan> ExecutePlanAsync(
        Plan plan,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing plan {PlanId} with {TaskCount} tasks", 
            plan.PlanId, plan.Tasks.Count);

        if (!context.IsWithinBudget())
        {
            _logger.LogWarning("Budget exceeded for plan {PlanId}", plan.PlanId);
            return plan with { Status = PlanStatus.Failed };
        }

        var updatedTasks = new List<TaskNode>();
        var planStartTime = DateTimeOffset.UtcNow;

        foreach (var task in plan.Tasks)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                updatedTasks.Add(task with { Status = TaskStatus.Cancelled });
                continue;
            }

            try
            {
                var updatedTask = await ExecuteTaskAsync(task, context, cancellationToken);
                updatedTasks.Add(updatedTask);
                
                if (updatedTask.Status == TaskStatus.Failed)
                {
                    _logger.LogWarning("Task {TaskId} failed: {Error}", 
                        task.TaskId, updatedTask.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing task {TaskId}", task.TaskId);
                updatedTasks.Add(task with 
                { 
                    Status = TaskStatus.Failed, 
                    ErrorMessage = ex.Message 
                });
            }
        }

        var finalStatus = DeterminePlanStatus(updatedTasks);
        
        return plan with
        {
            Tasks = updatedTasks,
            Status = finalStatus,
            StartedAt = planStartTime,
            CompletedAt = DateTimeOffset.UtcNow
        };
    }

    public async ValueTask<AgentId?> RouteRequestAsync(
        RequestEnvelope request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Routing request {CorrelationId}", request.CorrelationId);

        // Simple capability-based routing
        // In v1, this could use embeddings or LLM-based routing
        
        var allAgents = await _agentRegistry.GetAllDefinitionsAsync(cancellationToken);
        
        // Prefer orchestrator for complex requests
        var orchestrator = allAgents.FirstOrDefault(a => a.IsOrchestrator);
        if (orchestrator != null)
        {
            _logger.LogInformation("Routing to orchestrator: {AgentId}", orchestrator.Id);
            return orchestrator.Id;
        }

        // Fallback to first available agent
        var firstAgent = allAgents.FirstOrDefault();
        if (firstAgent != null)
        {
            _logger.LogInformation("Routing to agent: {AgentId}", firstAgent.Id);
            return firstAgent.Id;
        }

        _logger.LogWarning("No suitable agent found for request {CorrelationId}", request.CorrelationId);
        return null;
    }

    private async ValueTask<TaskNode> ExecuteTaskAsync(
        TaskNode task,
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing task {TaskId}", task.TaskId);

        // Find appropriate agent
        AgentId? agentId = task.AssignedAgent;
        
        if (agentId == null)
        {
            // Auto-assign based on task description (simplified for MVP)
            var allAgents = await _agentRegistry.GetAllDefinitionsAsync(cancellationToken);
            var agent = allAgents.FirstOrDefault(a => !a.IsOrchestrator);
            agentId = agent?.Id;
        }

        if (agentId == null)
        {
            return task with
            {
                Status = TaskStatus.Failed,
                ErrorMessage = "No suitable agent found"
            };
        }

        var definition = await _agentRegistry.GetDefinitionAsync(agentId.Value, cancellationToken);
        if (definition == null)
        {
            return task with
            {
                Status = TaskStatus.Failed,
                ErrorMessage = $"Agent {agentId} not found"
            };
        }

        try
        {
            var agent = await _agentFactory.CreateAgentAsync(definition, cancellationToken);
            var response = await agent.ExecuteAsync(task.Description, context, cancellationToken);

            context.TokensConsumed += response.TokensUsed;
            context.ToolCallsExecuted += response.ToolCallsExecuted;

            return task with
            {
                Status = response.IsSuccess ? TaskStatus.Completed : TaskStatus.Failed,
                Result = response.Content,
                ErrorMessage = response.ErrorMessage,
                AssignedAgent = agentId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute task {TaskId} with agent {AgentId}", 
                task.TaskId, agentId);
            
            return task with
            {
                Status = TaskStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
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

