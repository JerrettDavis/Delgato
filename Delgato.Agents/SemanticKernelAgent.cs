using Delgato.Core;
using Delgato.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ExecutionContext = Delgato.Core.ExecutionContext;

namespace Delgato.Agents;

/// <summary>
/// Agent implementation using Semantic Kernel for LLM interactions.
/// </summary>
public sealed class SemanticKernelAgent : IAgent
{
    private readonly Kernel _kernel;
    private readonly ILogger<SemanticKernelAgent> _logger;

    public AgentId Id { get; }
    public AgentDefinition Definition { get; }

    public SemanticKernelAgent(
        AgentId id,
        AgentDefinition definition,
        Kernel kernel,
        ILogger<SemanticKernelAgent> logger)
    {
        Id = id;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<AgentResponse> ExecuteAsync(
        string input,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Agent {AgentId} executing with input length: {Length}", 
            Id, input.Length);

        try
        {
            // Check budget before execution
            if (!context.IsWithinBudget())
            {
                _logger.LogWarning("Budget exceeded for agent {AgentId}", Id);
                return new AgentResponse
                {
                    Content = string.Empty,
                    IsSuccess = false,
                    ErrorMessage = "Budget exceeded"
                };
            }

            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();

            // Add system prompt if available
            var systemPrompt = await ResolvePromptAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                chatHistory.AddSystemMessage(systemPrompt);
            }

            // Add user input
            chatHistory.AddUserMessage(input);

            // Execute with SK
            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                cancellationToken: cancellationToken);

            var tokensUsed = response.Metadata?.TryGetValue("Usage", out var usage) == true
                ? ExtractTokenCount(usage)
                : 0;

            context.TokensConsumed += tokensUsed;

            return new AgentResponse
            {
                Content = response.Content ?? string.Empty,
                IsSuccess = true,
                TokensUsed = tokensUsed,
                ToolCallsExecuted = 0,
                Metadata = response.Metadata?.ToDictionary(kv => kv.Key, kv => kv.Value) 
                    ?? new Dictionary<string, object>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing agent {AgentId}", Id);
            return new AgentResponse
            {
                Content = string.Empty,
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async ValueTask<string> ResolvePromptAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(Definition.Prompt))
        {
            return Definition.Prompt;
        }

        if (Definition.PromptRef != null)
        {
            return Definition.PromptRef.Source switch
            {
                "file" => await File.ReadAllTextAsync(Definition.PromptRef.Value, cancellationToken),
                "url" => await DownloadPromptFromUrlAsync(Definition.PromptRef.Value, cancellationToken),
                "inline" => Definition.PromptRef.Value,
                _ => string.Empty
            };
        }

        return string.Empty;
    }

    private static async ValueTask<string> DownloadPromptFromUrlAsync(string url, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        return await httpClient.GetStringAsync(url, cancellationToken);
    }

    private static int ExtractTokenCount(object usage)
    {
        // Simplified token extraction - in production, parse the actual usage object
        return usage?.ToString()?.Contains("tokens") == true ? 100 : 0;
    }
}

