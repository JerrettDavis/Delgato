namespace Delgato.Core.Abstractions;

/// <summary>
/// Factory for creating agent instances.
/// </summary>
public interface IAgentFactory
{
    ValueTask<IAgent> CreateAgentAsync(
        AgentDefinition definition,
        CancellationToken cancellationToken = default);
}

