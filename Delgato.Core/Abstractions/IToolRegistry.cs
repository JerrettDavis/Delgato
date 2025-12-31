namespace Delgato.Core.Abstractions;

/// <summary>
/// Tool registry managing all available tools.
/// </summary>
public interface IToolRegistry
{
    ValueTask<ToolDefinition?> GetToolAsync(
        string toolId,
        CancellationToken cancellationToken = default);
    
    ValueTask<IReadOnlyList<ToolDefinition>> GetAllToolsAsync(
        CancellationToken cancellationToken = default);
    
    ValueTask RegisterToolAsync(
        ToolDefinition tool,
        CancellationToken cancellationToken = default);
    
    ValueTask<bool> IsToolAllowedAsync(
        string toolId,
        AgentId agentId,
        CancellationToken cancellationToken = default);
}

