using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Delgato.Core;
using Delgato.Core.Abstractions;
using Microsoft.Extensions.Logging;
using ExecutionContext = Delgato.Core.ExecutionContext;

namespace Delgato.Providers;

/// <summary>
/// Base class for agent providers with common functionality.
/// Implements retry logic, rate limiting, and health checks.
/// </summary>
public abstract class BaseAgentProvider : IAgentProvider
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;
    protected readonly ProviderOptions Options;

    protected BaseAgentProvider(
        HttpClient httpClient,
        ILogger logger,
        ProviderOptions options)
    {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public abstract string ProviderId { get; }

    /// <inheritdoc/>
    public abstract string DisplayName { get; }

    /// <inheritdoc/>
    public abstract IReadOnlyList<string> SupportedModels { get; }

    /// <inheritdoc/>
    public abstract IReadOnlyList<string> Capabilities { get; }

    /// <inheritdoc/>
    public virtual async Task<ProviderHealthResult> ValidateConnectionAsync(CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var isHealthy = await PerformHealthCheckAsync(ct);
            stopwatch.Stop();

            return new ProviderHealthResult
            {
                IsHealthy = isHealthy,
                Status = isHealthy ? "Healthy" : "Unhealthy",
                Latency = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Health check failed for provider {ProviderId}", ProviderId);
            return new ProviderHealthResult
            {
                IsHealthy = false,
                Status = "Error",
                ErrorMessage = ex.Message,
                Latency = stopwatch.Elapsed
            };
        }
    }

    /// <inheritdoc/>
    public async Task<AgentResponse> ExecuteAsync(
        AgentExecutionRequest request,
        ExecutionContext context,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        Logger.LogInformation(
            "Executing request on provider {ProviderId} with model {ModelId}",
            ProviderId,
            request.AgentDefinition.Model?.ModelId);

        try
        {
            var response = await ExecuteCoreAsync(request, context, ct);
            stopwatch.Stop();

            Logger.LogInformation(
                "Request completed on provider {ProviderId} in {Elapsed}ms with {Tokens} tokens",
                ProviderId,
                stopwatch.ElapsedMilliseconds,
                response.TokensUsed);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex,
                "Request failed on provider {ProviderId} after {Elapsed}ms",
                ProviderId,
                stopwatch.ElapsedMilliseconds);

            return new AgentResponse
            {
                Content = string.Empty,
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        AgentExecutionRequest request,
        ExecutionContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Default implementation: execute and return as single event
        var response = await ExecuteAsync(request, context, ct);

        if (response.IsSuccess)
        {
            yield return new AgentStreamEvent
            {
                Type = AgentStreamEventType.ContentDelta,
                Content = response.Content
            };

            yield return new AgentStreamEvent
            {
                Type = AgentStreamEventType.Complete,
                FinalResponse = response
            };
        }
        else
        {
            yield return new AgentStreamEvent
            {
                Type = AgentStreamEventType.Error,
                ErrorMessage = response.ErrorMessage
            };
        }
    }

    /// <inheritdoc/>
    public virtual Task<IReadOnlyList<ToolDefinition>> GetAvailableToolsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ToolDefinition>>(Array.Empty<ToolDefinition>());
    }

    /// <summary>
    /// Performs the actual health check for the provider.
    /// </summary>
    protected abstract Task<bool> PerformHealthCheckAsync(CancellationToken ct);

    /// <summary>
    /// Core execution logic for the provider.
    /// </summary>
    protected abstract Task<AgentResponse> ExecuteCoreAsync(
        AgentExecutionRequest request,
        ExecutionContext context,
        CancellationToken ct);

    /// <summary>
    /// Builds the system prompt from agent definition.
    /// </summary>
    protected string BuildSystemPrompt(AgentDefinition definition)
    {
        if (!string.IsNullOrEmpty(definition.Prompt))
        {
            return definition.Prompt;
        }

        // TODO: Handle PromptRef (file, url, inline)
        return $"You are {definition.Name}. {definition.Description ?? ""}";
    }

    /// <summary>
    /// Calculates cost for the request.
    /// </summary>
    protected virtual decimal CalculateCost(int inputTokens, int outputTokens, string modelId)
    {
        // Default implementation - override for specific pricing
        return 0m;
    }
}

/// <summary>
/// Options for configuring a provider.
/// </summary>
public class ProviderOptions
{
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public string? Organization { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(120);
    public int MaxRetries { get; set; } = 3;
    public bool EnableStreaming { get; set; } = true;
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}
