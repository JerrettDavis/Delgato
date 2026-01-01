using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delgato.Core;
using Delgato.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Delgato.Providers.OpenAI;

/// <summary>
/// OpenAI provider implementation.
/// Supports GPT-4, GPT-4o, o1, o3, and GPT-3.5 models.
/// </summary>
public sealed class OpenAIProvider : BaseAgentProvider
{
    private const string DefaultEndpoint = "https://api.openai.com/v1";

    private static readonly string[] _supportedModels = new[]
    {
        "gpt-4o",
        "gpt-4o-mini",
        "gpt-4-turbo",
        "gpt-4-turbo-preview",
        "gpt-4",
        "gpt-4-32k",
        "gpt-3.5-turbo",
        "gpt-3.5-turbo-16k",
        "o1-preview",
        "o1-mini",
        "o3-mini"
    };

    private static readonly string[] _capabilities = new[]
    {
        "chat",
        "streaming",
        "tools",
        "functions",
        "vision",
        "json-mode",
        "seed"
    };

    public OpenAIProvider(
        HttpClient httpClient,
        ILogger<OpenAIProvider> logger,
        ProviderOptions options)
        : base(httpClient, logger, options)
    {
        ConfigureHttpClient();
    }

    /// <inheritdoc/>
    public override string ProviderId => "openai";

    /// <inheritdoc/>
    public override string DisplayName => "OpenAI";

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedModels => _supportedModels;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Capabilities => _capabilities;

    private void ConfigureHttpClient()
    {
        var endpoint = Options.Endpoint ?? DefaultEndpoint;
        HttpClient.BaseAddress = new Uri(endpoint);
        HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {Options.ApiKey}");
        if (!string.IsNullOrEmpty(Options.Organization))
        {
            HttpClient.DefaultRequestHeaders.Add("OpenAI-Organization", Options.Organization);
        }
        HttpClient.Timeout = Options.Timeout;
    }

    /// <inheritdoc/>
    protected override async Task<bool> PerformHealthCheckAsync(CancellationToken ct)
    {
        try
        {
            var response = await HttpClient.GetAsync("/models", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    protected override async Task<AgentResponse> ExecuteCoreAsync(
        AgentExecutionRequest request,
        ExecutionContext context,
        CancellationToken ct)
    {
        var modelId = request.AgentDefinition.Model?.ModelId ?? "gpt-4o";
        var maxTokens = request.AgentDefinition.Model?.MaxTokens ?? 4096;
        var temperature = request.AgentDefinition.Model?.Temperature ?? 0.7;

        var openAIRequest = new OpenAIRequest
        {
            Model = modelId,
            MaxTokens = maxTokens,
            Temperature = temperature,
            Messages = BuildMessages(request)
        };

        // Add tools if available
        if (request.AvailableTools.Count > 0)
        {
            openAIRequest.Tools = request.AvailableTools.Select(t => new OpenAITool
            {
                Type = "function",
                Function = new OpenAIFunction
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = t.InputSchema
                }
            }).ToArray();
        }

        var response = await HttpClient.PostAsJsonAsync("/chat/completions", openAIRequest, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError("OpenAI API error: {StatusCode} - {Content}",
                response.StatusCode, responseContent);

            return new AgentResponse
            {
                Content = string.Empty,
                IsSuccess = false,
                ErrorMessage = $"OpenAI API error: {response.StatusCode}"
            };
        }

        var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (openAIResponse?.Choices == null || openAIResponse.Choices.Length == 0)
        {
            return new AgentResponse
            {
                Content = string.Empty,
                IsSuccess = false,
                ErrorMessage = "No response from OpenAI"
            };
        }

        var choice = openAIResponse.Choices[0];
        var content = choice.Message?.Content ?? string.Empty;
        var promptTokens = openAIResponse.Usage?.PromptTokens ?? 0;
        var completionTokens = openAIResponse.Usage?.CompletionTokens ?? 0;

        return new AgentResponse
        {
            Content = content,
            IsSuccess = true,
            TokensUsed = promptTokens + completionTokens,
            Metadata = new Dictionary<string, object>
            {
                ["model"] = openAIResponse.Model ?? modelId,
                ["prompt_tokens"] = promptTokens,
                ["completion_tokens"] = completionTokens,
                ["finish_reason"] = choice.FinishReason ?? "unknown"
            }
        };
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        AgentExecutionRequest request,
        ExecutionContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var modelId = request.AgentDefinition.Model?.ModelId ?? "gpt-4o";
        var maxTokens = request.AgentDefinition.Model?.MaxTokens ?? 4096;
        var temperature = request.AgentDefinition.Model?.Temperature ?? 0.7;

        var openAIRequest = new OpenAIRequest
        {
            Model = modelId,
            MaxTokens = maxTokens,
            Temperature = temperature,
            Messages = BuildMessages(request),
            Stream = true
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(openAIRequest),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await HttpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            yield return new AgentStreamEvent
            {
                Type = AgentStreamEventType.Error,
                ErrorMessage = $"OpenAI API error: {response.StatusCode}"
            };
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var contentBuilder = new StringBuilder();

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: "))
                continue;

            var data = line.Substring(6);
            if (data == "[DONE]")
                break;

            var streamEvent = JsonSerializer.Deserialize<OpenAIStreamResponse>(data,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (streamEvent?.Choices == null || streamEvent.Choices.Length == 0)
                continue;

            var delta = streamEvent.Choices[0].Delta;
            if (!string.IsNullOrEmpty(delta?.Content))
            {
                contentBuilder.Append(delta.Content);
                yield return new AgentStreamEvent
                {
                    Type = AgentStreamEventType.ContentDelta,
                    Content = delta.Content
                };
            }
        }

        yield return new AgentStreamEvent
        {
            Type = AgentStreamEventType.Complete,
            FinalResponse = new AgentResponse
            {
                Content = contentBuilder.ToString(),
                IsSuccess = true,
                Metadata = new Dictionary<string, object>
                {
                    ["model"] = modelId
                }
            }
        };
    }

    private OpenAIMessage[] BuildMessages(AgentExecutionRequest request)
    {
        var messages = new List<OpenAIMessage>();

        // Add system message
        var systemPrompt = BuildSystemPrompt(request.AgentDefinition);
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new OpenAIMessage { Role = "system", Content = systemPrompt });
        }

        // Add conversation history
        foreach (var msg in request.ConversationHistory)
        {
            var role = msg.Role switch
            {
                ChatRole.System => "system",
                ChatRole.User => "user",
                ChatRole.Assistant => "assistant",
                ChatRole.Tool => "tool",
                _ => "user"
            };

            messages.Add(new OpenAIMessage { Role = role, Content = msg.Content });
        }

        // Add current input
        messages.Add(new OpenAIMessage { Role = "user", Content = request.Input });

        return messages.ToArray();
    }

    protected override decimal CalculateCost(int inputTokens, int outputTokens, string modelId)
    {
        // OpenAI pricing (as of 2024)
        return modelId switch
        {
            "gpt-4o" => (inputTokens * 0.005m + outputTokens * 0.015m) / 1000,
            "gpt-4o-mini" => (inputTokens * 0.00015m + outputTokens * 0.0006m) / 1000,
            "gpt-4-turbo" => (inputTokens * 0.01m + outputTokens * 0.03m) / 1000,
            "gpt-4" => (inputTokens * 0.03m + outputTokens * 0.06m) / 1000,
            "gpt-3.5-turbo" => (inputTokens * 0.0005m + outputTokens * 0.0015m) / 1000,
            "o1-preview" => (inputTokens * 0.015m + outputTokens * 0.06m) / 1000,
            "o1-mini" => (inputTokens * 0.003m + outputTokens * 0.012m) / 1000,
            _ => 0m
        };
    }

    #region OpenAI API Models

    private class OpenAIRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("messages")]
        public required OpenAIMessage[] Messages { get; set; }

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }

        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }

        [JsonPropertyName("tools")]
        public OpenAITool[]? Tools { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private class OpenAIMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }

    private class OpenAITool
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public required OpenAIFunction Function { get; set; }
    }

    private class OpenAIFunction
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("parameters")]
        public object? Parameters { get; set; }
    }

    private class OpenAIResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("choices")]
        public OpenAIChoice[]? Choices { get; set; }

        [JsonPropertyName("usage")]
        public OpenAIUsage? Usage { get; set; }
    }

    private class OpenAIChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public OpenAIMessage? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private class OpenAIUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

    private class OpenAIStreamResponse
    {
        [JsonPropertyName("choices")]
        public OpenAIStreamChoice[]? Choices { get; set; }
    }

    private class OpenAIStreamChoice
    {
        [JsonPropertyName("delta")]
        public OpenAIDelta? Delta { get; set; }
    }

    private class OpenAIDelta
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    #endregion
}
