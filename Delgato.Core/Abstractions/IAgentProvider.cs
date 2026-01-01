namespace Delgato.Core.Abstractions;

/// <summary>
/// Core abstraction for pluggable agent providers (Claude, OpenAI, Gemini, etc.).
/// Providers handle the communication with specific LLM backends.
/// </summary>
public interface IAgentProvider
{
    /// <summary>
    /// Unique identifier for this provider (e.g., "anthropic", "openai", "gemini").
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Human-readable display name for the provider.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// List of model IDs supported by this provider.
    /// </summary>
    IReadOnlyList<string> SupportedModels { get; }

    /// <summary>
    /// Provider capabilities (e.g., "streaming", "tools", "vision", "hooks").
    /// </summary>
    IReadOnlyList<string> Capabilities { get; }

    /// <summary>
    /// Validates the provider connection and credentials.
    /// </summary>
    Task<ProviderHealthResult> ValidateConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// Executes a request against the provider.
    /// </summary>
    Task<AgentResponse> ExecuteAsync(
        AgentExecutionRequest request,
        ExecutionContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Streams responses from the provider.
    /// </summary>
    IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        AgentExecutionRequest request,
        ExecutionContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Gets available tools for this provider.
    /// </summary>
    Task<IReadOnlyList<ToolDefinition>> GetAvailableToolsAsync(CancellationToken ct = default);
}

/// <summary>
/// Request for agent execution.
/// </summary>
public sealed record AgentExecutionRequest
{
    public required string Input { get; init; }
    public required AgentDefinition AgentDefinition { get; init; }
    public IReadOnlyList<ChatMessage> ConversationHistory { get; init; } = Array.Empty<ChatMessage>();
    public IReadOnlyList<ToolDefinition> AvailableTools { get; init; } = Array.Empty<ToolDefinition>();
    public IDictionary<string, object> Parameters { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Chat message for conversation history.
/// </summary>
public sealed record ChatMessage
{
    public required ChatRole Role { get; init; }
    public required string Content { get; init; }
    public string? Name { get; init; }
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
}

/// <summary>
/// Chat message role.
/// </summary>
public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool
}

/// <summary>
/// Tool call in a message.
/// </summary>
public sealed record ToolCall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Arguments { get; init; }
    public string? Result { get; init; }
}

/// <summary>
/// Streaming event from agent execution.
/// </summary>
public sealed record AgentStreamEvent
{
    public required AgentStreamEventType Type { get; init; }
    public string? Content { get; init; }
    public ToolCall? ToolCall { get; init; }
    public AgentResponse? FinalResponse { get; init; }
    public string? ErrorMessage { get; init; }
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Types of streaming events.
/// </summary>
public enum AgentStreamEventType
{
    ContentDelta,
    ToolCallStart,
    ToolCallComplete,
    Complete,
    Error
}

/// <summary>
/// Provider health check result.
/// </summary>
public sealed record ProviderHealthResult
{
    public required bool IsHealthy { get; init; }
    public required string Status { get; init; }
    public TimeSpan? Latency { get; init; }
    public string? ErrorMessage { get; init; }
    public IDictionary<string, object> Details { get; init; } = new Dictionary<string, object>();
}
