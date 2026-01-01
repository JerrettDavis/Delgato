using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delgato.Core;
using Delgato.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Delgato.Providers.Anthropic;

/// <summary>
/// Anthropic Claude provider implementation.
/// Supports Claude 3.5, Claude 3, and Claude 2 models with full feature support.
/// </summary>
public sealed class ClaudeProvider : BaseAgentProvider
{
    private const string DefaultEndpoint = "https://api.anthropic.com";
    private const string ApiVersion = "2023-06-01";

    private static readonly string[] _supportedModels = new[]
    {
        "claude-3-5-sonnet-20241022",
        "claude-3-5-haiku-20241022",
        "claude-3-opus-20240229",
        "claude-3-sonnet-20240229",
        "claude-3-haiku-20240307",
        "claude-2.1",
        "claude-2.0"
    };

    private static readonly string[] _capabilities = new[]
    {
        "chat",
        "streaming",
        "tools",
        "vision",
        "long-context",
        "system-prompts"
    };

    public ClaudeProvider(
        HttpClient httpClient,
        ILogger<ClaudeProvider> logger,
        ProviderOptions options)
        : base(httpClient, logger, options)
    {
        ConfigureHttpClient();
    }

    /// <inheritdoc/>
    public override string ProviderId => "anthropic";

    /// <inheritdoc/>
    public override string DisplayName => "Anthropic Claude";

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedModels => _supportedModels;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Capabilities => _capabilities;

    private void ConfigureHttpClient()
    {
        var endpoint = Options.Endpoint ?? DefaultEndpoint;
        HttpClient.BaseAddress = new Uri(endpoint);
        HttpClient.DefaultRequestHeaders.Add("x-api-key", Options.ApiKey);
        HttpClient.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);
        HttpClient.Timeout = Options.Timeout;
    }

    /// <inheritdoc/>
    protected override async Task<bool> PerformHealthCheckAsync(CancellationToken ct)
    {
        try
        {
            // Send a minimal request to check connectivity
            var request = new ClaudeRequest
            {
                Model = "claude-3-haiku-20240307",
                MaxTokens = 1,
                Messages = new[]
                {
                    new ClaudeMessage { Role = "user", Content = "hi" }
                }
            };

            var response = await HttpClient.PostAsJsonAsync("/v1/messages", request, ct);
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
        var modelId = request.AgentDefinition.Model?.ModelId ?? "claude-3-5-sonnet-20241022";
        var maxTokens = request.AgentDefinition.Model?.MaxTokens ?? 4096;
        var temperature = request.AgentDefinition.Model?.Temperature ?? 0.7;

        var claudeRequest = new ClaudeRequest
        {
            Model = modelId,
            MaxTokens = maxTokens,
            Temperature = temperature,
            System = BuildSystemPrompt(request.AgentDefinition),
            Messages = BuildMessages(request)
        };

        // Add tools if available
        if (request.AvailableTools.Count > 0)
        {
            claudeRequest.Tools = request.AvailableTools.Select(t => new ClaudeTool
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = t.InputSchema
            }).ToArray();
        }

        var response = await HttpClient.PostAsJsonAsync("/v1/messages", claudeRequest, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError("Claude API error: {StatusCode} - {Content}",
                response.StatusCode, responseContent);

            return new AgentResponse
            {
                Content = string.Empty,
                IsSuccess = false,
                ErrorMessage = $"Claude API error: {response.StatusCode}"
            };
        }

        var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (claudeResponse == null)
        {
            return new AgentResponse
            {
                Content = string.Empty,
                IsSuccess = false,
                ErrorMessage = "Failed to parse Claude response"
            };
        }

        var content = ExtractContent(claudeResponse);
        var inputTokens = claudeResponse.Usage?.InputTokens ?? 0;
        var outputTokens = claudeResponse.Usage?.OutputTokens ?? 0;

        return new AgentResponse
        {
            Content = content,
            IsSuccess = true,
            TokensUsed = inputTokens + outputTokens,
            Metadata = new Dictionary<string, object>
            {
                ["model"] = claudeResponse.Model ?? modelId,
                ["input_tokens"] = inputTokens,
                ["output_tokens"] = outputTokens,
                ["stop_reason"] = claudeResponse.StopReason ?? "unknown"
            }
        };
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        AgentExecutionRequest request,
        ExecutionContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var modelId = request.AgentDefinition.Model?.ModelId ?? "claude-3-5-sonnet-20241022";
        var maxTokens = request.AgentDefinition.Model?.MaxTokens ?? 4096;
        var temperature = request.AgentDefinition.Model?.Temperature ?? 0.7;

        var claudeRequest = new ClaudeRequest
        {
            Model = modelId,
            MaxTokens = maxTokens,
            Temperature = temperature,
            System = BuildSystemPrompt(request.AgentDefinition),
            Messages = BuildMessages(request),
            Stream = true
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(claudeRequest),
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
                ErrorMessage = $"Claude API error: {response.StatusCode}"
            };
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var contentBuilder = new StringBuilder();
        int totalInputTokens = 0;
        int totalOutputTokens = 0;

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: "))
                continue;

            var data = line.Substring(6);
            if (data == "[DONE]")
                break;

            var streamEvent = JsonSerializer.Deserialize<ClaudeStreamEvent>(data,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (streamEvent == null)
                continue;

            switch (streamEvent.Type)
            {
                case "content_block_delta":
                    if (streamEvent.Delta?.Text != null)
                    {
                        contentBuilder.Append(streamEvent.Delta.Text);
                        yield return new AgentStreamEvent
                        {
                            Type = AgentStreamEventType.ContentDelta,
                            Content = streamEvent.Delta.Text
                        };
                    }
                    break;

                case "message_delta":
                    if (streamEvent.Usage != null)
                    {
                        totalOutputTokens = streamEvent.Usage.OutputTokens;
                    }
                    break;

                case "message_start":
                    if (streamEvent.Message?.Usage != null)
                    {
                        totalInputTokens = streamEvent.Message.Usage.InputTokens;
                    }
                    break;
            }
        }

        yield return new AgentStreamEvent
        {
            Type = AgentStreamEventType.Complete,
            FinalResponse = new AgentResponse
            {
                Content = contentBuilder.ToString(),
                IsSuccess = true,
                TokensUsed = totalInputTokens + totalOutputTokens,
                Metadata = new Dictionary<string, object>
                {
                    ["model"] = modelId,
                    ["input_tokens"] = totalInputTokens,
                    ["output_tokens"] = totalOutputTokens
                }
            }
        };
    }

    private ClaudeMessage[] BuildMessages(AgentExecutionRequest request)
    {
        var messages = new List<ClaudeMessage>();

        foreach (var msg in request.ConversationHistory)
        {
            var role = msg.Role switch
            {
                ChatRole.User => "user",
                ChatRole.Assistant => "assistant",
                _ => "user"
            };

            messages.Add(new ClaudeMessage { Role = role, Content = msg.Content });
        }

        // Add current input
        messages.Add(new ClaudeMessage { Role = "user", Content = request.Input });

        return messages.ToArray();
    }

    private string ExtractContent(ClaudeResponse response)
    {
        if (response.Content == null || response.Content.Length == 0)
            return string.Empty;

        var textContent = response.Content
            .Where(c => c.Type == "text")
            .Select(c => c.Text)
            .FirstOrDefault();

        return textContent ?? string.Empty;
    }

    protected override decimal CalculateCost(int inputTokens, int outputTokens, string modelId)
    {
        // Claude pricing (as of 2024)
        return modelId switch
        {
            "claude-3-opus-20240229" => (inputTokens * 0.015m + outputTokens * 0.075m) / 1000,
            "claude-3-5-sonnet-20241022" => (inputTokens * 0.003m + outputTokens * 0.015m) / 1000,
            "claude-3-sonnet-20240229" => (inputTokens * 0.003m + outputTokens * 0.015m) / 1000,
            "claude-3-haiku-20240307" => (inputTokens * 0.00025m + outputTokens * 0.00125m) / 1000,
            "claude-3-5-haiku-20241022" => (inputTokens * 0.001m + outputTokens * 0.005m) / 1000,
            _ => 0m
        };
    }

    #region Claude API Models

    private class ClaudeRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("system")]
        public string? System { get; set; }

        [JsonPropertyName("messages")]
        public required ClaudeMessage[] Messages { get; set; }

        [JsonPropertyName("tools")]
        public ClaudeTool[]? Tools { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private class ClaudeMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }

    private class ClaudeTool
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("input_schema")]
        public object? InputSchema { get; set; }
    }

    private class ClaudeResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public ClaudeContentBlock[]? Content { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }

        [JsonPropertyName("usage")]
        public ClaudeUsage? Usage { get; set; }
    }

    private class ClaudeContentBlock
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class ClaudeUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }

    private class ClaudeStreamEvent
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("delta")]
        public ClaudeDelta? Delta { get; set; }

        [JsonPropertyName("usage")]
        public ClaudeUsage? Usage { get; set; }

        [JsonPropertyName("message")]
        public ClaudeResponse? Message { get; set; }
    }

    private class ClaudeDelta
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    #endregion
}
