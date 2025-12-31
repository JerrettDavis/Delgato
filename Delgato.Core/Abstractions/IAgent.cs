namespace Delgato.Core.Abstractions;

/// <summary>
/// Core interface for executing agents.
/// </summary>
public interface IAgent
{
    AgentId Id { get; }
    AgentDefinition Definition { get; }
    
    ValueTask<AgentResponse> ExecuteAsync(
        string input,
        Core.ExecutionContext context,
        CancellationToken cancellationToken = default);
}

public sealed record AgentResponse
{
    public required string Content { get; init; }
    public bool IsSuccess { get; init; } = true;
    public string? ErrorMessage { get; init; }
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
    public int TokensUsed { get; init; }
    public int ToolCallsExecuted { get; init; }
}

