using Delgato.Agents;
using Delgato.Core;
using Delgato.Core.Abstractions;
using Microsoft.Extensions.Options;
using ExecutionContext = Delgato.Core.ExecutionContext;

namespace Delgato;

public class Worker(
    ILogger<Worker> logger,
    IAgentRegistry agentRegistry,
    IOrchestrator orchestrator,
    ITransportAdapter transportAdapter,
    IOptions<SwarmOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["Component"] = "Worker",
            ["StartTime"] = DateTimeOffset.UtcNow
        });

        logger.LogInformation("Delgato Agent Swarm starting at: {Time}", DateTimeOffset.UtcNow);

        try
        {
            // Initialize agent registry
            if (agentRegistry is FileBasedAgentRegistry fileRegistry)
            {
                logger.LogInformation("Initializing agent registry from directory: {Directory}", 
                    options.Value.AgentDirectory);

                await fileRegistry.InitializeAsync(stoppingToken);
                var definitions = await agentRegistry.GetAllDefinitionsAsync(stoppingToken);
                
                logger.LogInformation("Successfully loaded {AgentCount} agent definitions", definitions.Count);
                
                foreach (var def in definitions)
                {
                    logger.LogDebug("Registered agent: {AgentId} - {AgentName} with capabilities: {Capabilities}", 
                        def.Id, def.Name, string.Join(", ", def.Capabilities));
                }
            }

            // Start transport adapter
            logger.LogInformation("Starting transport adapter: {TransportName}", transportAdapter.Name);
            await transportAdapter.StartAsync(stoppingToken);
            logger.LogInformation("Transport adapter '{TransportName}' started successfully", transportAdapter.Name);

            // Process requests from transport
            logger.LogInformation("Worker ready to process requests");
            
            await foreach (var request in transportAdapter.ReceiveAsync(stoppingToken))
            {
                using var requestScope = logger.BeginScope(new Dictionary<string, object>
                {
                    ["CorrelationId"] = request.CorrelationId,
                    ["Source"] = request.Source
                });

                logger.LogInformation("Received request from {Source}", request.Source);

                // Process request asynchronously (fire and forget for now)
                _ = ProcessRequestAsync(request, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Worker shutdown requested");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Fatal error in worker - shutting down");
            throw;
        }
        finally
        {
            logger.LogInformation("Stopping transport adapter");
            await transportAdapter.StopAsync(CancellationToken.None);
            logger.LogInformation("Delgato Agent Swarm stopped at: {Time}", DateTimeOffset.UtcNow);
        }
    }

    private async Task ProcessRequestAsync(RequestEnvelope request, CancellationToken stoppingToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = request.CorrelationId,
            ["Source"] = request.Source
        });

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Processing request from {Source}", request.Source);

            // Create execution context
            var context = new ExecutionContext
            {
                CorrelationId = request.CorrelationId,
                Budget = Budget.Default,
                CancellationToken = stoppingToken
            };

            logger.LogDebug("Created execution context with default budget: MaxTokens={MaxTokens}, MaxDepth={MaxDepth}", 
                context.Budget.MaxTokens, context.Budget.MaxDepth);

            // Route to appropriate agent
            logger.LogDebug("Routing request to appropriate agent");
            var agentId = await orchestrator.RouteRequestAsync(request, stoppingToken);
            
            if (agentId == null)
            {
                logger.LogWarning("No suitable agent found for request - returning error");
                await transportAdapter.SendResponseAsync(
                    request.CorrelationId,
                    new { error = "No suitable agent found" },
                    stoppingToken);
                return;
            }

            logger.LogInformation("Request routed to agent: {AgentId}", agentId);

            // Create and execute plan
            logger.LogDebug("Creating execution plan");
            var plan = await orchestrator.CreatePlanAsync(request, context, stoppingToken);
            logger.LogInformation("Created plan {PlanId} with {TaskCount} tasks", 
                plan.PlanId, plan.Tasks.Count);

            logger.LogDebug("Executing plan {PlanId}", plan.PlanId);
            var executedPlan = await orchestrator.ExecutePlanAsync(plan, context, stoppingToken);
            
            stopwatch.Stop();

            logger.LogInformation(
                "Plan {PlanId} completed with status: {Status} in {Duration}ms. Tokens: {TokensUsed}, ToolCalls: {ToolCalls}", 
                executedPlan.PlanId, 
                executedPlan.Status, 
                stopwatch.ElapsedMilliseconds,
                context.TokensConsumed,
                context.ToolCallsExecuted);

            // Send response
            var response = new
            {
                planId = executedPlan.PlanId,
                status = executedPlan.Status.ToString(),
                tasks = executedPlan.Tasks.Select(t => new
                {
                    taskId = t.TaskId,
                    status = t.Status.ToString(),
                    result = t.Result,
                    error = t.ErrorMessage
                }).ToList(),
                tokensUsed = context.TokensConsumed,
                toolCalls = context.ToolCallsExecuted,
                durationMs = stopwatch.ElapsedMilliseconds
            };

            await transportAdapter.SendResponseAsync(request.CorrelationId, response, stoppingToken);
            await transportAdapter.AcknowledgeAsync(request.CorrelationId, stoppingToken);

            logger.LogInformation("Request {CorrelationId} completed successfully", request.CorrelationId);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Request {CorrelationId} cancelled", request.CorrelationId);
            await transportAdapter.SendResponseAsync(
                request.CorrelationId,
                new { error = "Request cancelled" },
                stoppingToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, 
                "Error processing request {CorrelationId} after {Duration}ms", 
                request.CorrelationId, 
                stopwatch.ElapsedMilliseconds);
            
            await transportAdapter.SendResponseAsync(
                request.CorrelationId,
                new { error = ex.Message, type = ex.GetType().Name },
                stoppingToken);
        }
    }
}