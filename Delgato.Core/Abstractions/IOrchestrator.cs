namespace Delgato.Core.Abstractions;

/// <summary>
/// Orchestrator responsible for planning, routing, and coordinating agent execution.
/// </summary>
public interface IOrchestrator
{
    ValueTask<Plan> CreatePlanAsync(
        RequestEnvelope request,
        ExecutionContext context,
        CancellationToken cancellationToken = default);
    
    ValueTask<Plan> ExecutePlanAsync(
        Plan plan,
        ExecutionContext context,
        CancellationToken cancellationToken = default);
    
    ValueTask<AgentId?> RouteRequestAsync(
        RequestEnvelope request,
        CancellationToken cancellationToken = default);
}

