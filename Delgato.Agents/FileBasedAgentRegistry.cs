using Delgato.Configuration;
using Delgato.Core;
using Delgato.Core.Abstractions;
using System.Collections.Concurrent;

namespace Delgato.Agents;

/// <summary>
/// File-based agent registry that loads definitions from a directory.
/// </summary>
public sealed class FileBasedAgentRegistry : IAgentRegistry
{
    private readonly ConcurrentDictionary<AgentId, AgentDefinition> _definitions = new();
    private readonly AgentFileLoader _loader;
    private readonly string _agentDirectory;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _isInitialized = false;

    public FileBasedAgentRegistry(string agentDirectory)
    {
        _agentDirectory = agentDirectory ?? throw new ArgumentNullException(nameof(agentDirectory));
        _loader = new AgentFileLoader();
    }

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
                return;

            if (!Directory.Exists(_agentDirectory))
            {
                // Create the directory if it doesn't exist
                Directory.CreateDirectory(_agentDirectory);
                // No agents to load yet
                _isInitialized = true;
                return;
            }

            var definitions = await _loader.LoadFromDirectoryAsync(_agentDirectory, cancellationToken);
            
            foreach (var definition in definitions)
            {
                _definitions[definition.Id] = definition;
            }
            
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(cancellationToken);
        }
    }

    public async ValueTask<AgentDefinition?> GetDefinitionAsync(
        AgentId agentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        _definitions.TryGetValue(agentId, out var definition);
        return definition;
    }

    public async ValueTask<IReadOnlyList<AgentDefinition>> GetAllDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _definitions.Values.ToList();
    }

    public async ValueTask<IReadOnlyList<AgentDefinition>> FindByCapabilityAsync(
        string capability,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var matches = _definitions.Values
            .Where(d => d.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return matches;
    }

    public ValueTask RegisterAsync(
        AgentDefinition definition,
        CancellationToken cancellationToken = default)
    {
        _definitions[definition.Id] = definition;
        return ValueTask.CompletedTask;
    }

    public ValueTask UnregisterAsync(
        AgentId agentId,
        CancellationToken cancellationToken = default)
    {
        _definitions.TryRemove(agentId, out _);
        return ValueTask.CompletedTask;
    }
}

