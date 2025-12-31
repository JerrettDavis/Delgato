using Delgato.Core;
using Delgato.Core.Abstractions;
using Microsoft.Extensions.Logging;
using ExecutionContext = Delgato.Core.ExecutionContext;

namespace Delgato.Dashboard.Web.Services;

/// <summary>
/// Service for executing agents and handling agent operations.
/// </summary>
public class AgentExecutionService
{
    private readonly IAgentFactory _agentFactory;
    private readonly IAgentRegistry _agentRegistry;
    private readonly ILogger<AgentExecutionService> _logger;

    public AgentExecutionService(
        IAgentFactory agentFactory,
        IAgentRegistry agentRegistry,
        ILogger<AgentExecutionService> logger)
    {
        _agentFactory = agentFactory;
        _agentRegistry = agentRegistry;
        _logger = logger;
    }

    public async Task<AgentExecutionResult> ExecuteAgentAsync(
        string agentId,
        string input,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing agent: {AgentId}", agentId);

        try
        {
            // Get agent definition
            var definition = await _agentRegistry.GetDefinitionAsync(new AgentId(agentId), cancellationToken);
            if (definition == null)
            {
                return new AgentExecutionResult
                {
                    Success = false,
                    Error = $"Agent '{agentId}' not found"
                };
            }

            // Create agent instance
            var agent = await _agentFactory.CreateAgentAsync(definition, cancellationToken);

            // Create execution context
            var startTime = DateTimeOffset.UtcNow;
            var context = new ExecutionContext
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Budget = definition.Budget ?? new Budget { MaxTokens = 10000 }
            };

            // Execute agent with input
            var response = await agent.ExecuteAsync(input, context, cancellationToken);

            return new AgentExecutionResult
            {
                Success = true,
                Output = response.Content ?? "No output",
                TokensUsed = response.TokensUsed,
                ExecutionTime = DateTimeOffset.UtcNow - startTime,
                CorrelationId = context.CorrelationId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing agent {AgentId}", agentId);
            return new AgentExecutionResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}

public class AgentExecutionResult
{
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public int TokensUsed { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public string? CorrelationId { get; set; }
}

