using Delgato.Transports.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Delgato.Api;

/// <summary>
/// HTTP endpoints for Delgato agent swarm.
/// </summary>
public static class DelgatoEndpoints
{
    public static void MapDelgatoEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api")
            .WithTags("Delgato")
            .WithOpenApi();

        // Health check
        api.MapGet("/health", () => Results.Ok(new
        {
            status = "healthy",
            service = "Delgato Agent Swarm",
            timestamp = DateTimeOffset.UtcNow
        }))
        .WithName("GetHealth")
        .WithSummary("Health check endpoint");

        // Submit request for processing
        api.MapPost("/execute", async (
            [FromBody] ExecuteRequest request,
            [FromServices] HttpTransportAdapter transport,
            [FromServices] ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            try
            {
                logger.LogInformation("API: Received execute request");

                var metadata = new Dictionary<string, string>
                {
                    ["apiVersion"] = "1.0",
                    ["clientIp"] = "unknown" // Would come from HttpContext in real implementation
                };

                if (!string.IsNullOrEmpty(request.UserId))
                {
                    metadata["userId"] = request.UserId;
                }

                var response = await transport.SubmitRequestAsync(
                    request.Payload ?? new { },
                    metadata,
                    cancellationToken);

                logger.LogInformation("API: Request processed successfully");

                return Results.Ok(response);
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex, "API: Request timed out");
                return Results.StatusCode(StatusCodes.Status408RequestTimeout);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "API: Error processing request");
                return Results.Problem(
                    title: "Request processing failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("ExecuteRequest")
        .WithSummary("Submit a request to the agent swarm for processing")
        .WithDescription("Submits a request that will be routed to the appropriate agent, planned, and executed.");

        // List available agents
        api.MapGet("/agents", async (
            [FromServices] Core.Abstractions.IAgentRegistry registry,
            [FromServices] ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            try
            {
                logger.LogDebug("API: Listing available agents");

                var definitions = await registry.GetAllDefinitionsAsync(cancellationToken);
                var agents = definitions.Select(d => new
                {
                    id = d.Id.Value,
                    name = d.Name,
                    description = d.Description,
                    capabilities = d.Capabilities,
                    isOrchestrator = d.IsOrchestrator,
                    allowedTools = d.AllowedTools,
                    budget = d.Budget != null ? new
                    {
                        maxTokens = d.Budget.MaxTokens,
                        maxToolCalls = d.Budget.MaxToolCalls,
                        maxDepth = d.Budget.MaxDepth,
                        maxDurationSeconds = d.Budget.MaxDuration?.TotalSeconds,
                        maxCost = d.Budget.MaxCost
                    } : null
                }).ToList();

                return Results.Ok(new
                {
                    count = agents.Count,
                    agents
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "API: Error listing agents");
                return Results.Problem(
                    title: "Failed to list agents",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("ListAgents")
        .WithSummary("List all available agents")
        .WithDescription("Returns information about all registered agents and their capabilities.");

        // Get specific agent
        api.MapGet("/agents/{agentId}", async (
            string agentId,
            [FromServices] Core.Abstractions.IAgentRegistry registry,
            [FromServices] ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            try
            {
                logger.LogDebug("API: Getting agent {AgentId}", agentId);

                var definition = await registry.GetDefinitionAsync(new Core.AgentId(agentId), cancellationToken);
                
                if (definition == null)
                {
                    return Results.NotFound(new { error = $"Agent '{agentId}' not found" });
                }

                var agent = new
                {
                    id = definition.Id.Value,
                    name = definition.Name,
                    description = definition.Description,
                    capabilities = definition.Capabilities,
                    isOrchestrator = definition.IsOrchestrator,
                    allowedTools = definition.AllowedTools,
                    model = definition.Model != null ? new
                    {
                        provider = definition.Model.Provider,
                        modelId = definition.Model.ModelId,
                        temperature = definition.Model.Temperature,
                        maxTokens = definition.Model.MaxTokens
                    } : null,
                    budget = definition.Budget != null ? new
                    {
                        maxTokens = definition.Budget.MaxTokens,
                        maxToolCalls = definition.Budget.MaxToolCalls,
                        maxDepth = definition.Budget.MaxDepth,
                        maxDurationSeconds = definition.Budget.MaxDuration?.TotalSeconds,
                        maxCost = definition.Budget.MaxCost
                    } : null
                };

                return Results.Ok(agent);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "API: Error getting agent {AgentId}", agentId);
                return Results.Problem(
                    title: "Failed to get agent",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("GetAgent")
        .WithSummary("Get information about a specific agent")
        .WithDescription("Returns detailed information about an agent by ID.");
    }
}

/// <summary>
/// Request model for /api/execute endpoint.
/// </summary>
public sealed record ExecuteRequest
{
    /// <summary>
    /// The request payload to process.
    /// </summary>
    public object? Payload { get; init; }

    /// <summary>
    /// Optional user ID for tracking.
    /// </summary>
    public string? UserId { get; init; }
}

