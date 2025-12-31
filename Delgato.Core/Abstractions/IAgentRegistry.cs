namespace Delgato.Core.Abstractions;

/// <summary>
/// Registry for managing agent definitions and instances.
/// </summary>
public interface IAgentRegistry
{
    ValueTask<AgentDefinition?> GetDefinitionAsync(
        AgentId agentId,
        CancellationToken cancellationToken = default);
    
    ValueTask<IReadOnlyList<AgentDefinition>> GetAllDefinitionsAsync(
        CancellationToken cancellationToken = default);
    
    ValueTask<IReadOnlyList<AgentDefinition>> FindByCapabilityAsync(
        string capability,
        CancellationToken cancellationToken = default);
    
    ValueTask RegisterAsync(
        AgentDefinition definition,
        CancellationToken cancellationToken = default);
    
    ValueTask UnregisterAsync(
        AgentId agentId,
        CancellationToken cancellationToken = default);
}

