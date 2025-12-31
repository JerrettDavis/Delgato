using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Delgato.Agents;

/// <summary>
/// Custom chat completion service for Anthropic Claude models.
/// </summary>
public sealed class ClaudeChatCompletionService : IChatCompletionService
{
    private readonly AnthropicClient _client;
    private readonly string _modelId;
    private readonly ILogger<ClaudeChatCompletionService> _logger;

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public ClaudeChatCompletionService(
        string apiKey,
        string modelId,
        ILogger<ClaudeChatCompletionService> logger)
    {
        _client = new AnthropicClient(new APIAuthentication(apiKey));
        _modelId = modelId;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Calling Claude API with model: {ModelId}", _modelId);

        try
        {
            // Convert SK chat history to Claude messages
            var messages = new List<Message>();
            string? systemPrompt = null;

            foreach (var message in chatHistory)
            {
                if (message.Role == AuthorRole.System)
                {
                    systemPrompt = message.Content;
                }
                else if (message.Role == AuthorRole.User)
                {
                    messages.Add(new Message(RoleType.User, message.Content ?? string.Empty));
                }
                else if (message.Role == AuthorRole.Assistant)
                {
                    messages.Add(new Message(RoleType.Assistant, message.Content ?? string.Empty));
                }
            }

            // Create message parameters
            var parameters = new MessageParameters
            {
                Messages = messages,
                Model = _modelId,
                MaxTokens = 4096,
                Stream = false,
                Temperature = GetTemperature(executionSettings)
            };

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                parameters.System = new List<SystemMessage>
                {
                    new SystemMessage(systemPrompt)
                };
            }

            // Call Claude API
            var response = await _client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);

            // Convert response to SK format - extract text from content blocks
            var content = string.Join("", response.Content
                .Where(c => c is Anthropic.SDK.Messaging.TextContent)
                .Cast<Anthropic.SDK.Messaging.TextContent>()
                .Select(c => c.Text));
            
            var metadata = new Dictionary<string, object?>
            {
                ["Id"] = response.Id,
                ["Model"] = response.Model,
                ["StopReason"] = response.StopReason,
                ["Usage"] = new
                {
                    InputTokens = response.Usage.InputTokens,
                    OutputTokens = response.Usage.OutputTokens,
                    TotalTokens = response.Usage.InputTokens + response.Usage.OutputTokens
                }
            };

            var chatMessage = new ChatMessageContent(
                AuthorRole.Assistant,
                content,
                _modelId,
                metadata: metadata);

            _logger.LogInformation("Claude API call successful. Tokens used: {TotalTokens}", 
                response.Usage.InputTokens + response.Usage.OutputTokens);

            return new List<ChatMessageContent> { chatMessage };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Claude API");
            throw;
        }
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Streaming Claude API call with model: {ModelId}", _modelId);

        // Convert SK chat history to Claude messages
        var messages = new List<Message>();
        string? systemPrompt = null;

        foreach (var message in chatHistory)
        {
            if (message.Role == AuthorRole.System)
            {
                systemPrompt = message.Content;
            }
            else if (message.Role == AuthorRole.User)
            {
                messages.Add(new Message(RoleType.User, message.Content ?? string.Empty));
            }
            else if (message.Role == AuthorRole.Assistant)
            {
                messages.Add(new Message(RoleType.Assistant, message.Content ?? string.Empty));
            }
        }

        // Create message parameters with streaming enabled
        var parameters = new MessageParameters
        {
            Messages = messages,
            Model = _modelId,
            MaxTokens = 4096,
            Stream = true,
            Temperature = GetTemperature(executionSettings)
        };

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            parameters.System = new List<SystemMessage>
            {
                new SystemMessage(systemPrompt)
            };
        }

        // Stream Claude API response
        await foreach (var streamResponse in _client.Messages.StreamClaudeMessageAsync(parameters, cancellationToken))
        {
            if (streamResponse.Delta?.Text != null)
            {
                yield return new StreamingChatMessageContent(
                    AuthorRole.Assistant,
                    streamResponse.Delta.Text,
                    metadata: new Dictionary<string, object?>
                    {
                        ["Model"] = _modelId
                    });
            }
        }
    }

    private static decimal GetTemperature(PromptExecutionSettings? settings)
    {
        if (settings is OpenAIPromptExecutionSettings openAISettings && openAISettings.Temperature.HasValue)
        {
            return (decimal)openAISettings.Temperature.Value;
        }
        return 0.7m; // Default temperature
    }
}
 