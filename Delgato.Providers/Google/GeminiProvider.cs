using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delgato.Core;
using Delgato.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Delgato.Providers.Google;

/// <summary>
/// Google Gemini provider implementation.
/// Supports Gemini Pro, Ultra, and Flash models.
/// </summary>
public sealed class GeminiProvider : BaseAgentProvider
{
    private const string DefaultEndpoint = "https://generativelanguage.googleapis.com/v1beta";

    private static readonly string[] _supportedModels = new[]
    {
        "gemini-2.0-flash-exp",
        "gemini-1.5-pro",
        "gemini-1.5-flash",
        "gemini-1.5-flash-8b",
        "gemini-1.0-pro",
        "gemini-pro",
        "gemini-pro-vision"
    };

    private static readonly string[] _capabilities = new[]
    {
        "chat",
        "streaming",
        "tools",
        "vision",
        "grounding",
        "code-execution"
    };

    public GeminiProvider(
        HttpClient httpClient,
        ILogger<GeminiProvider> logger,
        ProviderOptions options)
        : base(httpClient, logger, options)
    {
        ConfigureHttpClient();
    }

    /// <inheritdoc/>
    public override string ProviderId => "google";

    /// <inheritdoc/>
    public override string DisplayName => "Google Gemini";

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedModels => _supportedModels;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Capabilities => _capabilities;

    private void ConfigureHttpClient()
    {
        var endpoint = Options.Endpoint ?? DefaultEndpoint;
        HttpClient.BaseAddress = new Uri(endpoint);
        HttpClient.Timeout = Options.Timeout;
    }

    /// <inheritdoc/>
    protected override async Task<bool> PerformHealthCheckAsync(CancellationToken ct)
    {
        try
        {
            var response = await HttpClient.GetAsync($"/models?key={Options.ApiKey}", ct);
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
        var modelId = request.AgentDefinition.Model?.ModelId ?? "gemini-1.5-pro";
        var maxTokens = request.AgentDefinition.Model?.MaxTokens ?? 4096;
        var temperature = request.AgentDefinition.Model?.Temperature ?? 0.7;

        var geminiRequest = new GeminiRequest
        {
            Contents = BuildContents(request),
            GenerationConfig = new GeminiGenerationConfig
            {
                MaxOutputTokens = maxTokens,
                Temperature = temperature
            }
        };

        // Add system instruction
        var systemPrompt = BuildSystemPrompt(request.AgentDefinition);
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            geminiRequest.SystemInstruction = new GeminiContent
            {
                Parts = new[] { new GeminiPart { Text = systemPrompt } }
            };
        }

        // Add tools if available
        if (request.AvailableTools.Count > 0)
        {
            geminiRequest.Tools = new[]
            {
                new GeminiTool
                {
                    FunctionDeclarations = request.AvailableTools.Select(t => new GeminiFunctionDeclaration
                    {
                        Name = t.Name,
                        Description = t.Description,
                        Parameters = t.InputSchema
                    }).ToArray()
                }
            };
        }

        var url = $"/models/{modelId}:generateContent?key={Options.ApiKey}";
        var response = await HttpClient.PostAsJsonAsync(url, geminiRequest, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError("Gemini API error: {StatusCode} - {Content}",
                response.StatusCode, responseContent);

            return new AgentResponse
            {
                Content = string.Empty,
                IsSuccess = false,
                ErrorMessage = $"Gemini API error: {response.StatusCode}"
            };
        }

        var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (geminiResponse?.Candidates == null || geminiResponse.Candidates.Length == 0)
        {
            return new AgentResponse
            {
                Content = string.Empty,
                IsSuccess = false,
                ErrorMessage = "No response from Gemini"
            };
        }

        var candidate = geminiResponse.Candidates[0];
        var content = ExtractContent(candidate);
        var tokenCount = geminiResponse.UsageMetadata?.TotalTokenCount ?? 0;

        return new AgentResponse
        {
            Content = content,
            IsSuccess = true,
            TokensUsed = tokenCount,
            Metadata = new Dictionary<string, object>
            {
                ["model"] = modelId,
                ["prompt_tokens"] = geminiResponse.UsageMetadata?.PromptTokenCount ?? 0,
                ["completion_tokens"] = geminiResponse.UsageMetadata?.CandidatesTokenCount ?? 0,
                ["finish_reason"] = candidate.FinishReason ?? "unknown"
            }
        };
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        AgentExecutionRequest request,
        ExecutionContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var modelId = request.AgentDefinition.Model?.ModelId ?? "gemini-1.5-pro";
        var maxTokens = request.AgentDefinition.Model?.MaxTokens ?? 4096;
        var temperature = request.AgentDefinition.Model?.Temperature ?? 0.7;

        var geminiRequest = new GeminiRequest
        {
            Contents = BuildContents(request),
            GenerationConfig = new GeminiGenerationConfig
            {
                MaxOutputTokens = maxTokens,
                Temperature = temperature
            }
        };

        var systemPrompt = BuildSystemPrompt(request.AgentDefinition);
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            geminiRequest.SystemInstruction = new GeminiContent
            {
                Parts = new[] { new GeminiPart { Text = systemPrompt } }
            };
        }

        var url = $"/models/{modelId}:streamGenerateContent?key={Options.ApiKey}&alt=sse";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(geminiRequest),
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
                ErrorMessage = $"Gemini API error: {response.StatusCode}"
            };
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var contentBuilder = new StringBuilder();
        int totalTokens = 0;

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: "))
                continue;

            var data = line.Substring(6);
            var streamResponse = JsonSerializer.Deserialize<GeminiResponse>(data,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (streamResponse?.Candidates == null || streamResponse.Candidates.Length == 0)
                continue;

            var content = ExtractContent(streamResponse.Candidates[0]);
            if (!string.IsNullOrEmpty(content))
            {
                contentBuilder.Append(content);
                yield return new AgentStreamEvent
                {
                    Type = AgentStreamEventType.ContentDelta,
                    Content = content
                };
            }

            if (streamResponse.UsageMetadata != null)
            {
                totalTokens = streamResponse.UsageMetadata.TotalTokenCount;
            }
        }

        yield return new AgentStreamEvent
        {
            Type = AgentStreamEventType.Complete,
            FinalResponse = new AgentResponse
            {
                Content = contentBuilder.ToString(),
                IsSuccess = true,
                TokensUsed = totalTokens,
                Metadata = new Dictionary<string, object>
                {
                    ["model"] = modelId
                }
            }
        };
    }

    private GeminiContent[] BuildContents(AgentExecutionRequest request)
    {
        var contents = new List<GeminiContent>();

        foreach (var msg in request.ConversationHistory)
        {
            var role = msg.Role switch
            {
                ChatRole.User => "user",
                ChatRole.Assistant => "model",
                _ => "user"
            };

            contents.Add(new GeminiContent
            {
                Role = role,
                Parts = new[] { new GeminiPart { Text = msg.Content } }
            });
        }

        // Add current input
        contents.Add(new GeminiContent
        {
            Role = "user",
            Parts = new[] { new GeminiPart { Text = request.Input } }
        });

        return contents.ToArray();
    }

    private string ExtractContent(GeminiCandidate candidate)
    {
        if (candidate.Content?.Parts == null)
            return string.Empty;

        return string.Join("", candidate.Content.Parts
            .Where(p => !string.IsNullOrEmpty(p.Text))
            .Select(p => p.Text));
    }

    protected override decimal CalculateCost(int inputTokens, int outputTokens, string modelId)
    {
        // Gemini pricing (as of 2024)
        return modelId switch
        {
            "gemini-1.5-pro" => (inputTokens * 0.00125m + outputTokens * 0.005m) / 1000,
            "gemini-1.5-flash" => (inputTokens * 0.000075m + outputTokens * 0.0003m) / 1000,
            "gemini-1.0-pro" => (inputTokens * 0.0005m + outputTokens * 0.0015m) / 1000,
            _ => 0m
        };
    }

    #region Gemini API Models

    private class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public required GeminiContent[] Contents { get; set; }

        [JsonPropertyName("systemInstruction")]
        public GeminiContent? SystemInstruction { get; set; }

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; set; }

        [JsonPropertyName("tools")]
        public GeminiTool[]? Tools { get; set; }
    }

    private class GeminiContent
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("parts")]
        public required GeminiPart[] Parts { get; set; }
    }

    private class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class GeminiGenerationConfig
    {
        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }
    }

    private class GeminiTool
    {
        [JsonPropertyName("functionDeclarations")]
        public GeminiFunctionDeclaration[]? FunctionDeclarations { get; set; }
    }

    private class GeminiFunctionDeclaration
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("parameters")]
        public object? Parameters { get; set; }
    }

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public GeminiCandidate[]? Candidates { get; set; }

        [JsonPropertyName("usageMetadata")]
        public GeminiUsageMetadata? UsageMetadata { get; set; }
    }

    private class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }
    }

    private class GeminiUsageMetadata
    {
        [JsonPropertyName("promptTokenCount")]
        public int PromptTokenCount { get; set; }

        [JsonPropertyName("candidatesTokenCount")]
        public int CandidatesTokenCount { get; set; }

        [JsonPropertyName("totalTokenCount")]
        public int TotalTokenCount { get; set; }
    }

    #endregion
}
